using System.Security.Cryptography.X509Certificates;
using QRCoder;
using Taveloom.Host.Actions;
using Taveloom.Host.Native;
using Taveloom.Host.Security;
using Taveloom.Host.Server;
using Taveloom.Host.Storage;
using Taveloom.Host.Workspaces;
using Taveloom.Protocol;

namespace Taveloom.Host;

public sealed class HostOptions
{
    public string DataDir { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Taveloom");
    public int Port { get; init; } = 47821;
    /// <summary>False binds to loopback only (no phone can connect). True listens on the LAN, still restricted to private-range peers.</summary>
    public bool LanExposure { get; init; } = true;
    public IForegroundSource? Foreground { get; init; }
    public IInputSender? Input { get; init; }
    public IAudioController? Audio { get; init; }
    public string? ScreenshotDir { get; init; }
    public TimeSpan Debounce { get; init; } = TimeSpan.FromMilliseconds(400);
    public TimeSpan TelemetryInterval { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan AuthTimeout { get; init; } = TimeSpan.FromSeconds(15);
}

public sealed record PairingInfo(PairingQr Qr, DateTimeOffset ExpiresAt, byte[] QrPng);

/// <summary>Composition root and public surface of the Windows host. The WPF app and the integration tests both drive this class.</summary>
public sealed class TaveloomHost : IAsyncDisposable
{
    private readonly HostServer _server = new();
    private readonly HostContext _ctx;
    private readonly X509Certificate2 _cert;
    private readonly HostOptions _options;
    private Timer? _telemetryTimer;

    public Database Database { get; }
    public SettingsStore Settings { get; }
    public AuditLog Audit { get; }
    public DeviceRegistry Devices { get; }
    public PairingService Pairing { get; }
    public WorkspaceService Workspaces { get; }
    public ActionEngine Actions { get; }
    public AdaptiveEngine Adaptive { get; }
    public ActivityLog Activity { get; }
    public SessionRegistry Sessions { get; }
    public AuthThrottle Throttle { get; }
    public string Fingerprint { get; }
    public int Port => _server.Port;
    public string HostName { get; } = Environment.MachineName;
    /// <summary>True when the host accepts phones from the local network; false when bound to loopback only.</summary>
    public bool LanExposure => _options.LanExposure;

    public TaveloomHost(HostOptions? options = null)
    {
        _options = options ?? new HostOptions();
        Database = new Database(Path.Combine(_options.DataDir, "taveloom.db"));
        Settings = new SettingsStore(Database);
        Audit = new AuditLog(Database);
        Devices = new DeviceRegistry(Database, Audit);
        Pairing = new PairingService();
        Throttle = new AuthThrottle();
        Activity = new ActivityLog();
        Sessions = new SessionRegistry();

        var audio = _options.Audio ?? new CoreAudioController();
        var shots = _options.ScreenshotDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Taveloom");
        Actions = new ActionEngine(
        [
            new HotkeyHandler(_options.Input ?? new SendInputSender()),
            new LaunchHandler(ActionType.LaunchApp), new LaunchHandler(ActionType.LaunchFolder), new LaunchHandler(ActionType.LaunchUrl),
            new MediaHandler(_options.Input ?? new SendInputSender(), audio),
            new SystemHandler(shots),
        ]);
        Workspaces = new WorkspaceService(new WorkspaceStore(Database), Settings, Actions);
        Adaptive = new AdaptiveEngine(_options.Foreground ?? new WinEventForegroundSource(), Workspaces) { Debounce = _options.Debounce };

        _cert = HostCertificate.LoadOrCreate(_options.DataDir);
        Fingerprint = HostCertificate.Fingerprint(_cert);
        _ctx = new HostContext
        {
            HostName = HostName,
            Devices = Devices,
            Pairing = Pairing,
            Workspaces = Workspaces,
            Actions = Actions,
            Telemetry = new TelemetrySampler(audio),
            Audit = Audit,
            Throttle = Throttle,
            Activity = Activity,
            Sessions = Sessions,
            AuthTimeout = _options.AuthTimeout,
        };

        Workspaces.StateChanged += () => Sessions.Broadcast(() => Envelope.Create(MessageTypes.WorkspaceState, Workspaces.BuildState()));
        Devices.Revoked += id => _ = Sessions.DisconnectDeviceAsync(id,
            Envelope.Create(MessageTypes.DeviceRevoked, new ErrorPayload(ErrorCodes.DeviceRevoked, "This device was removed on the Windows PC.")));
    }

    public async Task StartAsync()
    {
        await _server.StartAsync(_ctx, _cert, _options.Port, _options.LanExposure);
        Adaptive.Start();
        // Telemetry is only sampled while at least one phone is connected.
        _telemetryTimer = new Timer(_ =>
        {
            if (Sessions.AuthenticatedCount == 0) return;
            var t = _ctx.Telemetry.Sample();
            Sessions.Broadcast(() => Envelope.Create(MessageTypes.Telemetry, t));
        }, null, _options.TelemetryInterval, _options.TelemetryInterval);
        Audit.Write("host.started", null, $"port {Port}");
    }

    /// <summary>Creates a fresh single-use pairing code and its QR image.</summary>
    public PairingInfo CreatePairing()
    {
        var offer = Pairing.CreateOffer();
        var hosts = _options.LanExposure ? NetworkGuard.LocalAddresses() : ["127.0.0.1"];
        var qr = new PairingQr(hosts.Count == 0 ? ["127.0.0.1"] : hosts, Port, Fingerprint, offer.Token, HostName);
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(qr.ToUri(), QRCodeGenerator.ECCLevel.M);
        return new PairingInfo(qr, offer.ExpiresAt, new PngByteQRCode(data).GetGraphic(12));
    }

    public async ValueTask DisposeAsync()
    {
        _telemetryTimer?.Dispose();
        Adaptive.Dispose();
        await _server.DisposeAsync();
        _cert.Dispose();
    }
}
