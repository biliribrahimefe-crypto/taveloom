using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Taveloom.Host.Native;
using Taveloom.Protocol;

namespace Taveloom.Host.Server;

/// <summary>Only loopback, RFC1918, link-local and IPv6 unique-local peers may talk to the host; it is never a public endpoint.</summary>
public static class NetworkGuard
{
    public static bool IsLocalNetwork(IPAddress? ip)
    {
        if (ip is null) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = ip.GetAddressBytes();
            return (b[0] & 0xFE) == 0xFC || (b[0] == 0xFE && (b[1] & 0xC0) == 0x80);
        }
        return false;
    }

    /// <summary>Private IPv4 addresses of this machine, for the pairing QR code.</summary>
    public static List<string> LocalAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is not NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses.Select(a => a.Address))
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork && IsLocalNetwork(a) && !a.ToString().StartsWith("169.254"))
            .Select(a => a.ToString()).Distinct().ToList();
}

/// <summary>Samples live values. Unavailable values are null rather than invented.</summary>
public sealed class TelemetrySampler(IAudioController audio)
{
    private readonly object _gate = new();
    private ulong _idle, _total;
    private bool _primed;

    public TelemetryPayload Sample()
    {
        double? cpu = null, ram = null;
        lock (_gate)
        {
            if (Win32.GetSystemTimes(out var i, out var k, out var u))
            {
                ulong idle = i.Value, total = k.Value + u.Value;
                if (_primed && total > _total) cpu = Math.Round(100.0 * (1.0 - (double)(idle - _idle) / (total - _total)), 1);
                _idle = idle; _total = total; _primed = true;
            }
        }
        var m = new Win32.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.MEMORYSTATUSEX>() };
        if (Win32.GlobalMemoryStatusEx(ref m)) ram = m.dwMemoryLoad;
        var a = audio.GetPlayback();
        return new TelemetryPayload(cpu is { } c ? Math.Clamp(c, 0, 100) : null, ram, a?.VolumePercent, a?.Muted, audio.GetMicrophoneMuted());
    }
}

public sealed record ActionRecord(DateTimeOffset At, string Device, string Control, bool Ok, string Message);

/// <summary>The most recent action results, shown on the dashboard.</summary>
public sealed class ActivityLog
{
    private readonly object _gate = new();
    private readonly LinkedList<ActionRecord> _items = new();
    public event Action<ActionRecord>? Added;

    public void Add(ActionRecord r)
    {
        lock (_gate) { _items.AddFirst(r); while (_items.Count > 50) _items.RemoveLast(); }
        Added?.Invoke(r);
    }

    public List<ActionRecord> Recent() { lock (_gate) return _items.ToList(); }
}
