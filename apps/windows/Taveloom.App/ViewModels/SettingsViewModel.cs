using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Taveloom.App.Services;
using Taveloom.Host;

namespace Taveloom.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private TaveloomHost Host => _main.Host;
    private readonly int _runningPort;
    private readonly bool _runningLan;
    private bool _loading = true;

    public string[] Themes { get; } = ["System", "Dark", "Light"];
    [ObservableProperty] private string _theme = "Dark";
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _lanExposure;
    [ObservableProperty] private string _portText = "";
    [ObservableProperty] private string? _portError;
    [ObservableProperty] private bool _autoSwitch;

    public string Version { get; } = Assembly.GetExecutingAssembly().GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.1.0";
    public string FingerprintFull => Host.Fingerprint;
    public string DataDir { get; }
    public bool RestartNeeded => !_loading && (LanExposure != _runningLan || (int.TryParse(PortText, out var p) && p != _runningPort));

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
        DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Taveloom");
        var s = Host.Settings;
        _theme = Capitalise(s.Get(Keys.Theme) ?? "dark");
        _startWithWindows = StartupService.IsEnabled();
        _minimizeToTray = s.GetBool(Keys.MinimizeToTray, false);
        _lanExposure = _runningLan = s.GetBool(Keys.Lan, true);
        _runningPort = Host.Port;
        _portText = (s.Get(Keys.Port) ?? Host.Port.ToString());
        _autoSwitch = Host.Workspaces.AutoEnabled;
        Host.Workspaces.StateChanged += () => Ui.Post(() => { _loading = true; AutoSwitch = Host.Workspaces.AutoEnabled; _loading = false; });
        _loading = false;
    }

    partial void OnThemeChanged(string value) { if (_loading) return; Host.Settings.Set(Keys.Theme, value.ToLowerInvariant()); ThemeService.Apply(value.ToLowerInvariant()); }
    private static string Capitalise(string v) => v.Length == 0 ? v : char.ToUpperInvariant(v[0]) + v[1..];
    partial void OnStartWithWindowsChanged(bool value) { if (!_loading) StartupService.Set(value); }
    partial void OnMinimizeToTrayChanged(bool value) { if (!_loading) Host.Settings.SetBool(Keys.MinimizeToTray, value); }
    partial void OnAutoSwitchChanged(bool value) { if (!_loading) Host.Workspaces.SetAuto(value); }

    partial void OnLanExposureChanged(bool value)
    {
        if (_loading) return;
        Host.Settings.SetBool(Keys.Lan, value);
        OnPropertyChanged(nameof(RestartNeeded));
    }

    partial void OnPortTextChanged(string value)
    {
        if (_loading) return;
        if (int.TryParse(value, out var p) && p is >= 1024 and <= 65535) { PortError = null; Host.Settings.Set(Keys.Port, p.ToString()); }
        else PortError = "Enter a port between 1024 and 65535.";
        OnPropertyChanged(nameof(RestartNeeded));
    }

    [RelayCommand] private void OpenDataFolder() => Process.Start(new ProcessStartInfo(DataDir) { UseShellExecute = true });
    [RelayCommand] private void ManageDevices() => _main.Navigate("Devices");
}
