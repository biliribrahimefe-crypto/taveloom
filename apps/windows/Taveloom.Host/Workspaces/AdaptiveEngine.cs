using System.Diagnostics;
using System.Text;
using Taveloom.Host.Native;

namespace Taveloom.Host.Workspaces;

public interface IForegroundSource : IDisposable
{
    event Action<ForegroundInfo>? Changed;
    ForegroundInfo? Current();
    void Start();
}

/// <summary>
/// Event-driven foreground tracking via SetWinEventHook(EVENT_SYSTEM_FOREGROUND): zero polling, zero CPU while the focus is unchanged.
/// The hook needs a message loop, so it runs on its own thread. Taveloom's own windows are skipped (WINEVENT_SKIPOWNPROCESS).
/// Limitation: only focus changes raise events, not window-title changes (e.g. switching browser tabs).
/// </summary>
public sealed class WinEventForegroundSource : IForegroundSource
{
    private Thread? _thread;
    private uint _threadId;
    private readonly Win32.WinEventProc _proc; // kept as a field so the GC cannot collect the native callback

    public event Action<ForegroundInfo>? Changed;

    public WinEventForegroundSource() => _proc = (_, _, hwnd, _, _, _, _) =>
    {
        if (Describe(hwnd) is { } info) Changed?.Invoke(info);
    };

    public ForegroundInfo? Current() => Describe(Win32.GetForegroundWindow());

    public void Start()
    {
        if (_thread is not null) return;
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _threadId = Win32.GetCurrentThreadId();
            var hook = Win32.SetWinEventHook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _proc, 0, 0,
                Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);
            ready.Set();
            if (hook == IntPtr.Zero) return;
            while (Win32.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0) { Win32.TranslateMessage(ref msg); Win32.DispatchMessage(ref msg); }
            Win32.UnhookWinEvent(hook);
        })
        { IsBackground = true, Name = "Taveloom.Foreground" };
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(2));
    }

    public void Dispose()
    {
        if (_thread is null) return;
        Win32.PostThreadMessage(_threadId, Win32.WM_QUIT, 0, 0);
        _thread.Join(TimeSpan.FromSeconds(1));
        _thread = null;
    }

    private static ForegroundInfo? Describe(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == Environment.ProcessId) return null;
        var h = Win32.OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
        if (h == IntPtr.Zero) return null; // e.g. elevated process: it cannot be identified, so no rule can match it
        try
        {
            var sb = new StringBuilder(1024); var size = (uint)sb.Capacity;
            if (!Win32.QueryFullProcessImageName(h, 0, sb, ref size)) return null;
            var title = new StringBuilder(256);
            Win32.GetWindowText(hwnd, title, title.Capacity);
            return new ForegroundInfo(Path.GetFileName(sb.ToString()), title.ToString());
        }
        finally { Win32.CloseHandle(h); }
    }
}

/// <summary>
/// Turns foreground changes into workspace switches. A change must stay stable for <see cref="Debounce"/> before it is applied,
/// so alt-tabbing through several windows does not flicker the phone through every workspace.
/// </summary>
public sealed class AdaptiveEngine : IDisposable
{
    private readonly IForegroundSource _source;
    private readonly WorkspaceService _workspaces;
    private readonly object _gate = new();
    private Timer? _timer;
    private ForegroundInfo? _last;

    public TimeSpan Debounce { get; init; } = TimeSpan.FromMilliseconds(400);
    public ForegroundInfo? LastForeground { get { lock (_gate) return _last; } }

    public AdaptiveEngine(IForegroundSource source, WorkspaceService workspaces)
    {
        _source = source; _workspaces = workspaces;
        _source.Changed += OnChanged;
        _workspaces.AutoResumed += EvaluateNow;
    }

    public void Start()
    {
        _source.Start();
        lock (_gate) _last = _source.Current();
        EvaluateNow();
    }

    private void OnChanged(ForegroundInfo info)
    {
        lock (_gate)
        {
            _last = info;
            if (!_workspaces.AutoEnabled || _workspaces.IsOverridden) return;
            _timer?.Dispose();
            _timer = new Timer(_ => EvaluateNow(), null, Debounce, Timeout.InfiniteTimeSpan);
        }
    }

    public void EvaluateNow()
    {
        ForegroundInfo? fg; lock (_gate) fg = _last;
        var match = WorkspaceResolver.Resolve(_workspaces.All, fg);
        if (match.Workspace is not null) _workspaces.ApplyAutomatic(match.Workspace.Id, match.Reason);
    }

    public void Dispose()
    {
        _source.Changed -= OnChanged;
        _workspaces.AutoResumed -= EvaluateNow;
        lock (_gate) { _timer?.Dispose(); _timer = null; }
        _source.Dispose();
    }
}
