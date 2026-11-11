using System.Drawing;
using System.Windows;
using Taveloom.App.Services;
using Taveloom.App.ViewModels;
using Taveloom.App.Views;
using Taveloom.Host;
using Taveloom.Host.Security;

namespace Taveloom.App;

public partial class App : Application
{
    private Mutex? _single;
    private TaveloomHost? _host;
    private MainWindow? _window;
    private System.Windows.Forms.NotifyIcon? _tray;
    private bool _quitting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _single = new Mutex(true, @"Local\Taveloom.SingleInstance", out var first);
        if (!first)
        {
            MessageBox.Show("Taveloom is already running. Look for it in the system tray.", "Taveloom", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(); return;
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show($"Something went wrong: {ex.Exception.Message}\n\nTaveloom will keep running.", "Taveloom", MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true;
        };

        // TAVELOOM_DATA_DIR / TAVELOOM_LAN=0 exist for development and screenshots: separate data, loopback-only (no firewall prompt).
        var dataDir = Environment.GetEnvironmentVariable("TAVELOOM_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Taveloom");
        try
        {
            var (port, lan) = UiSettings.ReadNetwork(dataDir);
            if (Environment.GetEnvironmentVariable("TAVELOOM_LAN") == "0") lan = false;
            _host = new TaveloomHost(new HostOptions { DataDir = dataDir, Port = port, LanExposure = lan });
            _host.Pairing.ApprovalRequested += p => Dispatcher.BeginInvoke(() => ShowApproval(p));
            await _host.StartAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Taveloom could not start its connection service.\n\n{ex.Message}\n\nIf another program uses the port, change it in Settings after fixing this, or close the other program and try again.",
                "Taveloom", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(); return;
        }

        ThemeService.Apply(_host.Settings.Get(Keys.Theme) ?? "dark");
        var vm = new MainViewModel(_host);
        vm.PairRequested += () => new PairDialog(_host!) { Owner = _window }.ShowDialog();
        if (Environment.GetEnvironmentVariable("TAVELOOM_PAGE") is { Length: > 0 } page) vm.Navigate(page); // dev/screenshots only
        _window = new MainWindow { DataContext = vm };
        _window.Closing += OnWindowClosing;
        SetupTray();

        var background = e.Args.Contains("--background") && _host.Settings.GetBool(Keys.MinimizeToTray, false);
        if (!background) _window.Show();
    }

    private void ShowApproval(PendingPairing p)
    {
        var dlg = new ApprovalDialog(p);
        void Close(string _) { if (_ == p.RequestId) Dispatcher.BeginInvoke(dlg.Close); }
        _host!.Pairing.ApprovalResolved += Close;
        dlg.Closed += (_, _) => { _host.Pairing.ApprovalResolved -= Close; _host.Pairing.Resolve(p.RequestId, dlg.Approved); };
        dlg.Show(); dlg.Activate();
    }

    private void SetupTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open Taveloom", null, (_, _) => ShowWindow());
        menu.Items.Add("Pair a phone", null, (_, _) => { ShowWindow(); _window?.Dispatcher.BeginInvoke(() => ((MainViewModel)_window.DataContext).RequestPairing()); });
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Quit());
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = Environment.ProcessPath is { } exe ? Icon.ExtractAssociatedIcon(exe) : SystemIcons.Application,
            Text = "Taveloom",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ShowWindow();
    }

    private void ShowWindow()
    {
        if (_window is null) return;
        _window.Show(); _window.WindowState = WindowState.Normal; _window.Activate();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_quitting || _host is null || !_host.Settings.GetBool(Keys.MinimizeToTray, false)) { Quit(); return; }
        e.Cancel = true; _window!.Hide();
    }

    private async void Quit()
    {
        if (_quitting) return;
        _quitting = true;
        _tray?.Dispose();
        if (_host is not null) await _host.DisposeAsync();
        _single?.ReleaseMutex();
        Shutdown();
    }
}
