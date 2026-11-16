// Test harness: a real TaveloomHost on loopback with fake input/audio and auto-approved pairing, so the Android (Dart)
// client can be tested against the real server without a phone. Not shipped.
// Usage: Taveloom.E2eHost <flagDir> [approveDelayMs]   Pairing is approved after the delay (a human clicking Allow). Creates "revoke.flag" in <flagDir> to revoke all devices; "quit.flag" to exit.
using Taveloom.Host;
using Taveloom.Host.Actions;
using Taveloom.Host.Native;
using Taveloom.Host.Workspaces;

var flagDir = args.Length > 0 ? args[0] : Path.GetTempPath();
Directory.CreateDirectory(flagDir);
var approveDelayMs = args.Length > 1 ? int.Parse(args[1]) : 0;
var dataDir = Path.Combine(Path.GetTempPath(), "taveloom-e2e-" + Guid.NewGuid().ToString("N"));

await using var host = new TaveloomHost(new HostOptions
{
    DataDir = dataDir,
    Port = 0,
    LanExposure = false,
    Input = new LoggingInput(),
    Audio = new FakeAudio(),
    Foreground = new IdleForeground(),
    ScreenshotDir = Path.Combine(dataDir, "shots"),
});
host.Pairing.ApprovalRequested += p => _ = Task.Run(async () =>
{
    await Task.Delay(approveDelayMs);
    Console.WriteLine($"APPROVE {p.DeviceName} after {approveDelayMs} ms");
    host.Pairing.Resolve(p.RequestId, true);
});
host.Sessions.Changed += () => Console.WriteLine($"SESSIONS {host.Sessions.AuthenticatedCount}");
await host.StartAsync();
Console.WriteLine("PAIR_URI=" + host.CreatePairing().Qr.ToUri());
Console.Out.Flush();

while (!File.Exists(Path.Combine(flagDir, "quit.flag")))
{
    if (File.Exists(Path.Combine(flagDir, "revoke.flag")))
    {
        File.Delete(Path.Combine(flagDir, "revoke.flag"));
        foreach (var d in host.Devices.List()) host.Devices.Revoke(d.Id);
        Console.WriteLine("REVOKED");
    }
    await Task.Delay(200);
}
try { Directory.Delete(dataDir, true); } catch { }

sealed class LoggingInput : IInputSender
{
    public void Send(KeyCombo combo) { Console.WriteLine($"KEY {combo.Key:X}"); Console.Out.Flush(); }
}

sealed class FakeAudio : IAudioController
{
    private AudioState _s = new(40, false);
    public AudioState? GetPlayback() => _s;
    public bool SetPlaybackVolume(double p) { _s = _s with { VolumePercent = Math.Round(p) }; return true; }
    public bool SetPlaybackMute(bool m) { _s = _s with { Muted = m }; return true; }
    public bool? GetMicrophoneMuted() => null;
}

sealed class IdleForeground : IForegroundSource
{
    public event Action<ForegroundInfo>? Changed { add { } remove { } }
    public ForegroundInfo? Current() => null;
    public void Start() { }
    public void Dispose() { }
}
