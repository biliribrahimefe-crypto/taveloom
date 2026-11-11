using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Taveloom.Host;

namespace Taveloom.App.Views;

public partial class PairDialog : Window
{
    private readonly TaveloomHost _host;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly int _devicesAtStart;
    private DateTimeOffset _expires;

    public PairDialog(TaveloomHost host)
    {
        InitializeComponent();
        Services.ThemeService.Hook(this);
        _host = host;
        _devicesAtStart = host.Devices.List().Count;
        _timer.Tick += (_, _) => Tick();
        host.Devices.Changed += OnDevicesChanged;
        Closed += (_, _) => { _timer.Stop(); host.Devices.Changed -= OnDevicesChanged; };
        NewCode();
    }

    private void NewCode()
    {
        var info = _host.CreatePairing();
        _expires = info.ExpiresAt;
        var bmp = new BitmapImage();
        bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.StreamSource = new MemoryStream(info.QrPng); bmp.EndInit(); bmp.Freeze();
        QrImage.Source = bmp;
        Expired.Visibility = Visibility.Collapsed;
        SetStatus("Waiting for your phone…", false);
        Addresses.Text = _host.LanExposure
            ? $"Host: {string.Join(", ", info.Qr.Hosts.Select(h => $"{h}:{info.Qr.Port}"))}"
            : "Local-network access is off, so no phone can connect. Turn it on in Settings > Connection.";
        _timer.Start(); Tick();
    }

    private void Tick()
    {
        var left = _expires - DateTimeOffset.UtcNow;
        if (left <= TimeSpan.Zero) { Countdown.Text = "Expired"; Expired.Visibility = Visibility.Visible; _timer.Stop(); return; }
        Countdown.Text = $"Code expires in {left.Minutes}:{left.Seconds:00}";
    }

    private void OnDevicesChanged() => Dispatcher.BeginInvoke(() =>
    {
        if (_host.Devices.List().Count > _devicesAtStart || _host.Sessions.AuthenticatedCount > 0)
            SetStatus("Phone paired and connected.", true);
    });

    private void SetStatus(string text, bool ok)
    {
        Status.Text = text;
        StatusBox.Background = (System.Windows.Media.Brush)FindResource(ok ? "AccentSoft" : "SurfaceAlt");
    }

    private void OnNewCode(object sender, RoutedEventArgs e) => NewCode();
    private void OnDone(object sender, RoutedEventArgs e) => Close();
}
