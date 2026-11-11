using System.Net;
using System.Net.WebSockets;
using System.Threading.Channels;
using Taveloom.Host.Actions;
using Taveloom.Host.Security;
using Taveloom.Host.Storage;
using Taveloom.Host.Workspaces;
using Taveloom.Protocol;

namespace Taveloom.Host.Server;

/// <summary>Everything a session needs from the host. One instance per running host.</summary>
public sealed class HostContext
{
    public required string HostName { get; init; }
    public required DeviceRegistry Devices { get; init; }
    public required PairingService Pairing { get; init; }
    public required WorkspaceService Workspaces { get; init; }
    public required ActionEngine Actions { get; init; }
    public required TelemetrySampler Telemetry { get; init; }
    public required AuditLog Audit { get; init; }
    public required AuthThrottle Throttle { get; init; }
    public required ActivityLog Activity { get; init; }
    public required SessionRegistry Sessions { get; init; }
    public TimeSpan AuthTimeout { get; init; } = TimeSpan.FromSeconds(15);
}

public sealed class SessionRegistry
{
    private readonly object _gate = new();
    private readonly List<ClientSession> _sessions = [];
    public event Action? Changed;

    public void Add(ClientSession s) { lock (_gate) _sessions.Add(s); Changed?.Invoke(); }
    public void Remove(ClientSession s) { lock (_gate) _sessions.Remove(s); Changed?.Invoke(); }
    public List<ClientSession> Authenticated() { lock (_gate) return _sessions.Where(s => s.Authenticated).ToList(); }
    public int AuthenticatedCount { get { lock (_gate) return _sessions.Count(s => s.Authenticated); } }
    public void Notify() => Changed?.Invoke();

    public void Broadcast(Func<Envelope> make) { var e = make(); foreach (var s in Authenticated()) s.Send(e); }

    public async Task DisconnectDeviceAsync(string deviceId, Envelope? farewell = null)
    {
        List<ClientSession> targets; lock (_gate) targets = _sessions.Where(s => s.DeviceId == deviceId).ToList();
        foreach (var s in targets) await s.CloseAsync(farewell);
    }
}

/// <summary>One WebSocket connection: pairing or authentication first, then actions and state sync.</summary>
public sealed class ClientSession
{
    private enum Phase { Anonymous, Challenged, Authenticated }

    private readonly HostContext _ctx;
    private readonly WebSocket _ws;
    private readonly string _remote;
    private readonly Channel<byte[]> _outbox = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait });
    private readonly CancellationTokenSource _life = new();
    private readonly Dictionary<string, Task<ActionResultPayload>> _requests = new();
    private readonly Queue<string> _requestOrder = new();
    private Phase _phase = Phase.Anonymous;
    private string? _challengeNonce, _challengeDevice;
    private int _violations;
    private double _tokens = 20; private long _lastRefill = Environment.TickCount64;

    public ClientSession(HostContext ctx, WebSocket ws, IPAddress? remote) { _ctx = ctx; _ws = ws; _remote = remote?.ToString() ?? "unknown"; }

    public string? DeviceId { get; private set; }
    public string? DeviceName { get; private set; }
    public bool Authenticated => _phase == Phase.Authenticated;
    public long LastAckedRevision { get; private set; }
    public string Remote => _remote;

    public void Send(Envelope e)
    {
        if (_ws.State == WebSocketState.Open && !_outbox.Writer.TryWrite(e.ToUtf8()))
            _ = CloseAsync(null); // consumer too slow: drop the connection rather than buffer without bound
    }

    public async Task CloseAsync(Envelope? farewell)
    {
        if (farewell is not null) { _outbox.Writer.TryWrite(farewell.ToUtf8()); await Task.Delay(100); }
        _outbox.Writer.TryComplete();
        _life.CancelAfter(TimeSpan.FromSeconds(1));
    }

    public async Task RunAsync(CancellationToken serverStopping)
    {
        using var authTimer = new CancellationTokenSource(_ctx.AuthTimeout);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(serverStopping, _life.Token, authTimer.Token);
        _ctx.Sessions.Add(this);
        var writer = WriteLoopAsync(cts.Token);
        try
        {
            while (_ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
            {
                var data = await ReceiveTextAsync(cts.Token);
                if (data is null) break;
                if (!Envelope.TryParse(data, out var env, out var perr))
                {
                    SendError(ErrorCodes.InvalidMessage, perr);
                    if (++_violations >= 5) break;
                    continue;
                }
                var wasAuthenticated = Authenticated;
                await HandleAsync(env, cts.Token);
                if (!wasAuthenticated && Authenticated) authTimer.CancelAfter(Timeout.InfiniteTimeSpan);
                if (_phase == Phase.Anonymous && _violations >= 3) break;
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            _outbox.Writer.TryComplete();
            try { await Task.WhenAny(writer, Task.Delay(TimeSpan.FromSeconds(1))); } catch { } // let queued errors/farewells flush
            _life.Cancel();
            try { await writer; } catch { }
            _ctx.Sessions.Remove(this);
            if (_ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
                try { await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); } catch { }
        }
    }

    private async Task<byte[]?> ReceiveTextAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();
        while (true)
        {
            var r = await _ws.ReceiveAsync(buffer, ct);
            if (r.MessageType == WebSocketMessageType.Close) return null;
            if (r.MessageType != WebSocketMessageType.Text)
            {
                await _ws.CloseOutputAsync(WebSocketCloseStatus.InvalidMessageType, "Text frames only", CancellationToken.None);
                return null;
            }
            ms.Write(buffer, 0, r.Count);
            if (ms.Length > ProtocolInfo.MaxMessageBytes)
            {
                await _ws.CloseOutputAsync(WebSocketCloseStatus.MessageTooBig, "Message too large", CancellationToken.None);
                return null;
            }
            if (r.EndOfMessage) return ms.ToArray();
        }
    }

    private async Task WriteLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var bytes in _outbox.Reader.ReadAllAsync(ct))
            {
                if (_ws.State != WebSocketState.Open) break;
                await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally { _life.Cancel(); }
    }

    private void SendError(string code, string message, string? requestId = null) =>
        Send(Envelope.Create(MessageTypes.Error, new ErrorPayload(code, message, requestId), requestId));

    private async Task HandleAsync(Envelope env, CancellationToken ct)
    {
        if (env.Type == MessageTypes.Ping) { Send(Envelope.Empty(MessageTypes.Pong, env.Id)); return; }

        if (_phase != Phase.Authenticated)
        {
            switch (env.Type)
            {
                case MessageTypes.PairRequest when _phase == Phase.Anonymous: await HandlePairAsync(env, ct); return;
                case MessageTypes.AuthHello when _phase == Phase.Anonymous: HandleHello(env); return;
                case MessageTypes.AuthResponse when _phase == Phase.Challenged: await HandleAuthResponseAsync(env); return;
                default:
                    _violations++;
                    SendError(ErrorCodes.Unauthenticated, "Authenticate before sending this message.", env.Id);
                    return;
            }
        }

        switch (env.Type)
        {
            case MessageTypes.StateAck when env.TryReadPayload<StateAckPayload>(out var ack):
                LastAckedRevision = Math.Max(LastAckedRevision, ack.Revision); break;
            case MessageTypes.WorkspaceSelect when env.TryReadPayload<WorkspaceSelectPayload>(out var sel):
                {
                    var before = _ctx.Workspaces.Revision;
                    if (!_ctx.Workspaces.SelectManual(sel.WorkspaceId)) SendError(ErrorCodes.UnknownWorkspace, "That workspace no longer exists.", env.Id);
                    else ReplyWithStateIfUnchanged(before); // a change is already broadcast to every phone
                    break;
                }
            case MessageTypes.WorkspaceAuto when env.TryReadPayload<WorkspaceAutoPayload>(out var auto):
                {
                    var before = _ctx.Workspaces.Revision;
                    _ctx.Workspaces.SetAuto(auto.Enabled);
                    ReplyWithStateIfUnchanged(before);
                    break;
                }
            case MessageTypes.ActionInvoke when env.TryReadPayload<ActionInvokePayload>(out var inv):
                HandleInvoke(env, inv); break;
            default:
                SendError(ErrorCodes.InvalidMessage, $"Unsupported or malformed message '{env.Type}'.", env.Id); break;
        }
    }

    private void ReplyWithStateIfUnchanged(long revisionBefore)
    {
        if (_ctx.Workspaces.Revision == revisionBefore) Send(Envelope.Create(MessageTypes.WorkspaceState, _ctx.Workspaces.BuildState()));
    }

    private async Task HandlePairAsync(Envelope env, CancellationToken ct)
    {
        if (_ctx.Throttle.IsBlocked(_remote)) { SendError(ErrorCodes.RateLimited, "Too many failed attempts. Try again in a minute."); _violations = 3; return; }
        if (!env.TryReadPayload<PairRequestPayload>(out var p) || !DeviceRegistry.IsValidDeviceId(p.DeviceId) || string.IsNullOrWhiteSpace(p.DeviceName))
        { SendError(ErrorCodes.InvalidMessage, "Invalid pairing request.", env.Id); _violations++; return; }

        switch (_ctx.Pairing.Consume(p.Token))
        {
            case TokenResult.Unknown:
                _ctx.Throttle.RecordFailure(_remote); _ctx.Audit.Write("pair.invalid_token", null, _remote);
                SendError(ErrorCodes.AuthFailed, "This pairing code is not valid. Generate a new QR code on Windows.", env.Id); _violations = 3; return;
            case TokenResult.Expired:
                _ctx.Audit.Write("pair.expired", null, _remote);
                SendError(ErrorCodes.PairingExpired, "This pairing code has expired. Generate a new QR code on Windows.", env.Id); _violations = 3; return;
        }

        Send(Envelope.Empty(MessageTypes.PairPending, env.Id));
        var approved = await _ctx.Pairing.RequestApprovalAsync(p.DeviceId, p.DeviceName, _remote, ct);
        if (!approved)
        {
            _ctx.Audit.Write("pair.rejected", p.DeviceId, _remote);
            Send(Envelope.Empty(MessageTypes.PairRejected, env.Id));
            SendError(ErrorCodes.PairingRejected, "The pairing request was declined on the Windows PC.", env.Id);
            _violations = 3; return;
        }

        var credential = _ctx.Devices.AddOrReplace(p.DeviceId, p.DeviceName);
        Send(Envelope.Create(MessageTypes.PairApproved,
            new PairApprovedPayload(p.DeviceId, System.Buffers.Text.Base64Url.EncodeToString(credential), _ctx.HostName), env.Id));
        BecomeAuthenticated(p.DeviceId, p.DeviceName);
    }

    private void HandleHello(Envelope env)
    {
        if (_ctx.Throttle.IsBlocked(_remote)) { SendError(ErrorCodes.RateLimited, "Too many failed attempts. Try again in a minute."); _violations = 3; return; }
        if (!env.TryReadPayload<AuthHelloPayload>(out var hello) || !DeviceRegistry.IsValidDeviceId(hello.DeviceId))
        { SendError(ErrorCodes.InvalidMessage, "Invalid hello.", env.Id); _violations++; return; }

        if (_ctx.Devices.GetCredential(hello.DeviceId) is null)
        {
            if (_ctx.Devices.IsRevoked(hello.DeviceId))
            { SendError(ErrorCodes.DeviceRevoked, "This device was removed on the Windows PC. Pair it again.", env.Id); }
            else { _ctx.Throttle.RecordFailure(_remote); _ctx.Audit.Write("auth.unknown_device", null, _remote); SendError(ErrorCodes.AuthFailed, "This device is not paired with this PC.", env.Id); }
            _violations = 3; return;
        }
        _challengeDevice = hello.DeviceId;
        _challengeNonce = AuthProof.RandomToken(24);
        _phase = Phase.Challenged;
        Send(Envelope.Create(MessageTypes.AuthChallenge, new AuthChallengePayload(_challengeNonce), env.Id));
    }

    private Task HandleAuthResponseAsync(Envelope env)
    {
        var cred = _challengeDevice is null ? null : _ctx.Devices.GetCredential(_challengeDevice);
        if (!env.TryReadPayload<AuthResponsePayload>(out var r) || cred is null || !AuthProof.Verify(cred, _challengeNonce!, _challengeDevice!, r.Proof))
        {
            _ctx.Throttle.RecordFailure(_remote); _ctx.Audit.Write("auth.failed", _challengeDevice, _remote);
            SendError(ErrorCodes.AuthFailed, "Authentication failed. Pair this device again.", env.Id);
            _phase = Phase.Anonymous; _violations = 3;
            return Task.CompletedTask;
        }
        _ctx.Throttle.Reset(_remote);
        var name = _ctx.Devices.List().FirstOrDefault(d => d.Id == _challengeDevice)?.Name ?? "Android device";
        Send(Envelope.Create(MessageTypes.AuthOk, new AuthOkPayload(_ctx.HostName, ProtocolInfo.Version), env.Id));
        BecomeAuthenticated(_challengeDevice!, name);
        return Task.CompletedTask;
    }

    private void BecomeAuthenticated(string deviceId, string name)
    {
        DeviceId = deviceId; DeviceName = name; _phase = Phase.Authenticated;
        _ctx.Devices.Touch(deviceId);
        _ctx.Audit.Write("session.start", deviceId, _remote);
        Send(Envelope.Create(MessageTypes.WorkspaceState, _ctx.Workspaces.BuildState()));
        Send(Envelope.Create(MessageTypes.Telemetry, _ctx.Telemetry.Sample()));
        _ctx.Sessions.Notify();
    }

    private bool TakeToken()
    {
        var now = Environment.TickCount64;
        _tokens = Math.Min(20, _tokens + (now - _lastRefill) / 1000.0 * 10);
        _lastRefill = now;
        if (_tokens < 1) return false;
        _tokens--; return true;
    }

    private void HandleInvoke(Envelope env, ActionInvokePayload inv)
    {
        var requestId = env.Id;
        if (string.IsNullOrWhiteSpace(requestId)) { SendError(ErrorCodes.InvalidMessage, "action.invoke needs a request id.", null); return; }

        if (_requests.TryGetValue(requestId, out var existing))
        {   // duplicate request id: replay the stored outcome, never run the action twice
            _ = existing.ContinueWith(t => Send(Envelope.Create(MessageTypes.ActionResult, t.Result, requestId)), TaskContinuationOptions.OnlyOnRanToCompletion);
            return;
        }
        if (!TakeToken()) { SendError(ErrorCodes.RateLimited, "Slow down: too many actions per second.", requestId); return; }

        var task = ExecuteAsync(requestId, inv);
        _requests[requestId] = task;
        _requestOrder.Enqueue(requestId);
        while (_requestOrder.Count > 128) _requests.Remove(_requestOrder.Dequeue());
        _ = task.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && Authenticated) Send(Envelope.Create(MessageTypes.ActionResult, t.Result, requestId));
        });
    }

    private async Task<ActionResultPayload> ExecuteAsync(string requestId, ActionInvokePayload inv)
    {
        long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var control = _ctx.Workspaces.Active?.Controls.FirstOrDefault(c => c.Id == inv.ControlId);
        if (control is null)
            return new ActionResultPayload(requestId, inv.ControlId, false, Now(),
                new ErrorPayload(ErrorCodes.UnknownControl, "That control is not in the active workspace (it may have just changed).", requestId), null, null);

        var outcome = await _ctx.Actions.ExecuteAsync(control, inv.Value, _life.Token);
        _ctx.Activity.Add(new ActionRecord(DateTimeOffset.UtcNow, DeviceName ?? "device", control.Label, outcome.Ok, outcome.Message));
        _ctx.Audit.Write(outcome.Ok ? "action.ok" : "action.failed", DeviceId, $"{control.Id} {outcome.ErrorCode}".Trim());
        return new ActionResultPayload(requestId, control.Id, outcome.Ok, Now(),
            outcome.Ok ? null : new ErrorPayload(outcome.ErrorCode ?? ErrorCodes.ActionFailed, outcome.Message, requestId),
            outcome.Ok ? outcome.Message : null, _ctx.Telemetry.Sample());
    }
}
