using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Taveloom.Host;
using Taveloom.Host.Actions;
using Taveloom.Host.Native;
using Taveloom.Host.Workspaces;
using Taveloom.Protocol;

namespace Taveloom.Host.Tests;

public sealed class FakeInput : IInputSender
{
    public List<KeyCombo> Sent { get; } = [];
    public bool Block { get; set; }
    public void Send(KeyCombo combo) { if (Block) throw new InputBlockedException("blocked"); lock (Sent) Sent.Add(combo); }
}

public sealed class FakeAudio : IAudioController
{
    public AudioState? State { get; set; } = new(40, false);
    public AudioState? GetPlayback() => State;
    public bool SetPlaybackVolume(double p) { if (State is null) return false; State = State.Value with { VolumePercent = Math.Round(p) }; return true; }
    public bool SetPlaybackMute(bool m) { if (State is null) return false; State = State.Value with { Muted = m }; return true; }
    public bool? GetMicrophoneMuted() => null;
}

public sealed class FakeForeground : IForegroundSource
{
    public event Action<ForegroundInfo>? Changed;
    public ForegroundInfo? Initial { get; set; }
    public ForegroundInfo? Current() => Initial;
    public void Start() { }
    public void Raise(string process, string title = "") => Changed?.Invoke(new ForegroundInfo(process, title));
    public void Dispose() { }
}

/// <summary>A real TaveloomHost on loopback with an ephemeral port, a temp data dir, and fake native back-ends.</summary>
public sealed class TestHost : IAsyncDisposable
{
    public TaveloomHost Host { get; private set; } = null!;
    public FakeInput Input { get; } = new();
    public FakeAudio Audio { get; } = new();
    public FakeForeground Foreground { get; } = new();
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "taveloom-test-" + Guid.NewGuid().ToString("N"));

    public static async Task<TestHost> StartAsync(bool autoApprove = true, TimeSpan? authTimeout = null)
    {
        var t = new TestHost();
        t.Host = new TaveloomHost(new HostOptions
        {
            DataDir = t.Dir,
            Port = 0,
            LanExposure = false,
            Foreground = t.Foreground,
            Input = t.Input,
            Audio = t.Audio,
            ScreenshotDir = Path.Combine(t.Dir, "shots"),
            Debounce = TimeSpan.FromMilliseconds(60),
            TelemetryInterval = TimeSpan.FromMilliseconds(200),
            AuthTimeout = authTimeout ?? TimeSpan.FromSeconds(15),
        });
        if (autoApprove) t.Host.Pairing.ApprovalRequested += p => t.Host.Pairing.Resolve(p.RequestId, true);
        await t.Host.StartAsync();
        return t;
    }

    public string NewDeviceId() => "dev-" + Guid.NewGuid().ToString("N")[..16];

    public async Task<(TestClient Client, string DeviceId, byte[] Credential)> PairAsync(string name = "Test Phone")
    {
        var qr = Host.CreatePairing().Qr;
        var c = await TestClient.ConnectAsync(Host.Port, qr.Fingerprint);
        var id = NewDeviceId();
        await c.SendAsync(MessageTypes.PairRequest, new PairRequestPayload(qr.Token, id, name), "p1");
        await c.ExpectAsync(MessageTypes.PairPending);
        var approved = (await c.ExpectAsync(MessageTypes.PairApproved)).Payload!.Value.Deserialize<PairApprovedPayload>(ProtocolJson.Options)!;
        await c.ExpectAsync(MessageTypes.WorkspaceState);
        return (c, id, System.Buffers.Text.Base64Url.DecodeFromChars(approved.Credential));
    }

    /// <summary>Authenticates a fresh connection with a stored credential (challenge-response).</summary>
    public async Task<TestClient> LoginAsync(string deviceId, byte[] credential, string? pinOverride = null)
    {
        var c = await TestClient.ConnectAsync(Host.Port, pinOverride ?? Host.Fingerprint);
        await c.SendAsync(MessageTypes.AuthHello, new AuthHelloPayload(deviceId));
        var ch = (await c.ExpectAsync(MessageTypes.AuthChallenge)).Payload!.Value.Deserialize<AuthChallengePayload>(ProtocolJson.Options)!;
        await c.SendAsync(MessageTypes.AuthResponse, new AuthResponsePayload(AuthProof.Compute(credential, ch.Nonce, deviceId)));
        await c.ExpectAsync(MessageTypes.AuthOk);
        await c.ExpectAsync(MessageTypes.WorkspaceState);
        return c;
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        try { Directory.Delete(Dir, true); } catch { /* SQLite may hold the file briefly; the temp dir is disposable */ }
    }
}

public sealed class TestClient : IAsyncDisposable
{
    private readonly ClientWebSocket _ws;
    private TestClient(ClientWebSocket ws) => _ws = ws;
    public WebSocketState State => _ws.State;

    /// <summary>Connects the way the Android app must: trust only the certificate whose SHA-256 matches the pinned fingerprint.</summary>
    public static async Task<TestClient> ConnectAsync(int port, string pinnedFingerprint)
    {
        var ws = new ClientWebSocket();
        ws.Options.RemoteCertificateValidationCallback = (_, cert, _, _) =>
            cert is not null && Convert.ToHexString(SHA256.HashData(cert.GetRawCertData())) == pinnedFingerprint.ToUpperInvariant();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await ws.ConnectAsync(new Uri($"wss://127.0.0.1:{port}/ws"), cts.Token);
        return new TestClient(ws);
    }

    public Task SendAsync<T>(string type, T payload, string? id = null) => SendRawAsync(Envelope.Create(type, payload, id).ToUtf8());

    public Task SendRawAsync(byte[] bytes, WebSocketMessageType kind = WebSocketMessageType.Text) =>
        _ws.SendAsync(bytes, kind, true, CancellationToken.None);

    public Task SendTextAsync(string text) => SendRawAsync(Encoding.UTF8.GetBytes(text));

    /// <summary>Next message, or null if the socket closed.</summary>
    public async Task<Envelope?> ReceiveAsync(int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var buf = new byte[65536]; using var ms = new MemoryStream();
        try
        {
            WebSocketReceiveResult r;
            do
            {
                r = await _ws.ReceiveAsync(buf, cts.Token);
                if (r.MessageType == WebSocketMessageType.Close) return null;
                ms.Write(buf, 0, r.Count);
            } while (!r.EndOfMessage);
        }
        catch (WebSocketException) { return null; }
        catch (OperationCanceledException) { throw new TimeoutException("No message received in time."); }
        Assert.True(Envelope.TryParse(ms.ToArray(), out var env, out var err), err);
        return env;
    }

    /// <summary>Reads until a message of the given type arrives (skipping telemetry and other noise).</summary>
    public async Task<Envelope> ExpectAsync(string type, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var e = await ReceiveAsync(Math.Max(100, (int)(deadline - DateTime.UtcNow).TotalMilliseconds))
                ?? throw new Exception($"Socket closed while waiting for '{type}'.");
            if (e.Type == type) return e;
            if (e.Type == MessageTypes.Error && type != MessageTypes.Error) throw new Exception($"Got error while waiting for '{type}': {e.Payload}");
        }
        throw new TimeoutException($"Timed out waiting for '{type}'.");
    }

    public static T Read<T>(Envelope e) where T : class { Assert.True(e.TryReadPayload<T>(out var v)); return v; }

    public async Task<bool> WaitClosedAsync(int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try { if (await ReceiveAsync(500) is null) return true; } catch (TimeoutException) { }
        }
        return _ws.State is not WebSocketState.Open;
    }

    public void Abort() => _ws.Abort();
    public async ValueTask DisposeAsync() { _ws.Abort(); _ws.Dispose(); await Task.CompletedTask; }
}
