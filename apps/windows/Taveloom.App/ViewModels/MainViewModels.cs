using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Taveloom.App.Services;
using Taveloom.Host;
using Taveloom.Host.Security;

namespace Taveloom.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public TaveloomHost Host { get; }
    public DashboardViewModel Dashboard { get; }
    public DevicesViewModel Devices { get; }
    public StudioViewModel Studio { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty] private string _page = "Dashboard";
    [ObservableProperty] private ObservableObject _current;

    public event Action? PairRequested;

    public MainViewModel(TaveloomHost host)
    {
        Host = host;
        Dashboard = new DashboardViewModel(this);
        Devices = new DevicesViewModel(this);
        Studio = new StudioViewModel(this);
        Settings = new SettingsViewModel(this);
        _current = Dashboard;
    }

    [RelayCommand]
    public void Navigate(string page)
    {
        Page = page;
        Current = page switch { "Devices" => Devices, "Studio" => Studio, "Settings" => Settings, _ => Dashboard };
    }

    public void RequestPairing() => PairRequested?.Invoke();
}

public sealed partial class WorkspaceItem(string id, string name, string icon, bool active) : ObservableObject
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Icon { get; } = icon;
    public bool IsActive { get; } = active;
}

public sealed record ActionRow(string Time, string Device, string Control, string Result, string Status);

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private TaveloomHost Host => _main.Host;

    [ObservableProperty] private string _hostStatus = "";
    [ObservableProperty] private string _hostStatusKind = "ok";
    [ObservableProperty] private string _fingerprint = "";
    [ObservableProperty] private string _deviceSummary = "";
    [ObservableProperty] private string _deviceKind = "idle";
    [ObservableProperty] private string _activeName = "";
    [ObservableProperty] private string _activeReason = "";
    [ObservableProperty] private string _modeText = "";
    [ObservableProperty] private bool _autoEnabled;
    [ObservableProperty] private bool _recentEmpty = true;
    private bool _refreshing;

    public ObservableCollection<WorkspaceItem> Workspaces { get; } = [];
    public ObservableCollection<ActionRow> Recent { get; } = [];
    public ObservableCollection<string> ConnectedDevices { get; } = [];

    public DashboardViewModel(MainViewModel main)
    {
        _main = main;
        Host.Workspaces.StateChanged += () => Ui.Post(Refresh);
        Host.Sessions.Changed += () => Ui.Post(Refresh);
        Host.Devices.Changed += () => Ui.Post(Refresh);
        Host.Activity.Added += _ => Ui.Post(RefreshActions);
        Refresh(); RefreshActions();
    }

    partial void OnAutoEnabledChanged(bool value)
    {
        if (_refreshing) return;
        Host.Workspaces.SetAuto(value);
    }

    public void Refresh()
    {
        _refreshing = true;
        try
        {
            var lan = Host.LanExposure;
            HostStatus = lan ? $"Listening on port {Host.Port} (local network)" : $"Listening on port {Host.Port} (this PC only, phones cannot connect)";
            HostStatusKind = lan ? "ok" : "warn";
            Fingerprint = string.Join(' ', Enumerable.Range(0, 4).Select(i => Host.Fingerprint.Substring(i * 4, 4)));

            var live = Host.Sessions.Authenticated();
            ConnectedDevices.Clear();
            foreach (var s in live) ConnectedDevices.Add(s.DeviceName ?? "Android device");
            var trusted = Host.Devices.List().Count(d => !d.Revoked);
            DeviceSummary = live.Count > 0 ? $"{live.Count} connected" : trusted == 0 ? "No devices paired yet" : $"{trusted} paired, none connected";
            DeviceKind = live.Count > 0 ? "ok" : "idle";

            var ws = Host.Workspaces;
            ActiveName = ws.Active?.Name ?? "None";
            ActiveReason = ws.Reason;
            AutoEnabled = ws.AutoEnabled;
            ModeText = ws.AutoEnabled && !ws.IsOverridden ? "Automatic" : ws.AutoEnabled ? "Manual override" : "Manual";
            Workspaces.Clear();
            foreach (var w in ws.All) Workspaces.Add(new WorkspaceItem(w.Id, w.Name, w.Icon, w.Id == ws.ActiveId));
        }
        finally { _refreshing = false; }
    }

    private void RefreshActions()
    {
        Recent.Clear();
        foreach (var r in Host.Activity.Recent().Take(8))
            Recent.Add(new ActionRow(r.At.LocalDateTime.ToString("HH:mm:ss"), r.Device, r.Control, r.Message, r.Ok ? "ok" : "bad"));
        RecentEmpty = Recent.Count == 0;
    }

    [RelayCommand] private void SelectWorkspace(WorkspaceItem w) => Host.Workspaces.SelectManual(w.Id);
    [RelayCommand] private void ResumeAuto() => Host.Workspaces.SetAuto(true);
    [RelayCommand] private void Pair() => _main.RequestPairing();
    [RelayCommand] private void OpenStudio() => _main.Navigate("Studio");
}

public sealed partial class DeviceItem : ObservableObject
{
    public string Id { get; init; } = "";
    [ObservableProperty] private string _name = "";
    public string Paired { get; init; } = "";
    public string LastSeen { get; init; } = "";
    public bool IsConnected { get; init; }
    public bool IsRevoked { get; init; }
    public string Status => IsRevoked ? "Revoked" : IsConnected ? "Connected" : "Offline";
    public string StatusKind => IsRevoked ? "bad" : IsConnected ? "ok" : "idle";
}

public sealed partial class DevicesViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private TaveloomHost Host => _main.Host;
    public ObservableCollection<DeviceItem> Devices { get; } = [];
    public ObservableCollection<string> Checks { get; } = [];
    [ObservableProperty] private bool _isEmpty;

    public DevicesViewModel(MainViewModel main)
    {
        _main = main;
        Host.Devices.Changed += () => Ui.Post(Refresh);
        Host.Sessions.Changed += () => Ui.Post(Refresh);
        Refresh();
    }

    public void Refresh()
    {
        var live = Host.Sessions.Authenticated().Select(s => s.DeviceId).ToHashSet();
        Devices.Clear();
        foreach (var d in Host.Devices.List())
            Devices.Add(new DeviceItem
            {
                Id = d.Id,
                Name = d.Name,
                IsRevoked = d.Revoked,
                IsConnected = live.Contains(d.Id),
                Paired = $"Paired {d.PairedAt.LocalDateTime:g}",
                LastSeen = d.LastSeen is { } t ? $"Last seen {t.LocalDateTime:g}" : "Never connected",
            });
        IsEmpty = Devices.Count == 0;

        Checks.Clear();
        var lan = Host.LanExposure;
        Checks.Add($"Host is listening on port {Host.Port}.");
        Checks.Add(lan ? "Local-network access is on." : "Local-network access is OFF, so phones cannot connect. Turn it on in Settings > Connection and restart Taveloom.");
        var addrs = Taveloom.Host.Server.NetworkGuard.LocalAddresses();
        Checks.Add(addrs.Count > 0 ? $"This PC's addresses: {string.Join(", ", addrs)}" : "No private network address found. Connect this PC to Wi-Fi or Ethernet.");
        Checks.Add("The phone and this PC must be on the same network. If pairing times out, allow Taveloom through Windows Firewall on private networks.");
    }

    [RelayCommand] private void Pair() => _main.RequestPairing();

    [RelayCommand]
    private async Task Disconnect(DeviceItem d) => await Host.Sessions.DisconnectDeviceAsync(d.Id);

    [RelayCommand]
    private void Revoke(DeviceItem d)
    {
        if (System.Windows.MessageBox.Show($"Revoke \"{d.Name}\"?\n\nIt will be disconnected and must be paired again to control this PC.",
                "Revoke device", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.OK)
            Host.Devices.Revoke(d.Id);
    }

    [RelayCommand]
    private void Forget(DeviceItem d) => Host.Devices.Forget(d.Id);

    public void Rename(DeviceItem d)
    {
        if (!string.IsNullOrWhiteSpace(d.Name)) Host.Devices.Rename(d.Id, d.Name);
    }
}
