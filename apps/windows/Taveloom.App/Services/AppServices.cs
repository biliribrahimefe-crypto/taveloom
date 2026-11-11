using System.Windows;
using Microsoft.Win32;
using Taveloom.Host.Storage;

namespace Taveloom.App.Services;

public static class Keys
{
    public const string Theme = "ui.theme", StartWithWindows = "ui.startWithWindows", MinimizeToTray = "ui.minimizeToTray";
    public const string Lan = "net.lan", Port = "net.port";
}

public static class ThemeService
{
    private static bool _dark = true;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Keeps the native title bar in step with the app theme (Windows 11 / recent Windows 10).</summary>
    public static void Hook(Window w)
    {
        w.SourceInitialized += (_, _) => TitleBar(w);
    }

    private static void TitleBar(Window w)
    {
        var h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
        if (h == IntPtr.Zero) return;
        var v = _dark ? 1 : 0;
        DwmSetWindowAttribute(h, 20, ref v, sizeof(int));
    }

    /// <summary>Applies "system", "dark" or "light" by swapping the first merged dictionary (all brushes are DynamicResource).</summary>
    public static void Apply(string mode)
    {
        var dark = mode switch { "dark" => true, "light" => false, _ => SystemUsesDarkTheme() };
        var dicts = Application.Current.Resources.MergedDictionaries;
        dicts[0] = new ResourceDictionary { Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative) };
        _dark = dark;
        foreach (Window w in Application.Current.Windows) TitleBar(w);
    }

    private static bool SystemUsesDarkTheme()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return k?.GetValue("AppsUseLightTheme") is int v ? v == 0 : true;
    }
}

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue("Taveloom") is string;
    }

    public static void Set(bool enabled)
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled && Environment.ProcessPath is { } exe) k.SetValue("Taveloom", $"\"{exe}\" --background");
        else k.DeleteValue("Taveloom", throwOnMissingValue: false);
    }
}

/// <summary>Maps the portable icon names used in workspace files to Segoe Fluent Icons glyphs.</summary>
public static class IconGlyphs
{
    public static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>
    {
        ["code"] = "",
        ["terminal"] = "",
        ["folder"] = "",
        ["globe"] = "",
        ["camera"] = "",
        ["play_pause"] = "",
        ["skip_next"] = "",
        ["skip_previous"] = "",
        ["volume"] = "",
        ["mute"] = "",
        ["lock"] = "",
        ["file"] = "",
        ["calculator"] = "",
        ["note"] = "",
        ["cpu"] = "",
        ["memory"] = "",
        ["mic"] = "",
        ["star"] = "",
        ["briefcase"] = "",
        ["music"] = "",
        ["download"] = "",
        ["link"] = "",
        ["settings"] = "",
        ["keyboard"] = "",
        ["home"] = "",
    };

    public static IReadOnlyList<string> Names { get; } = Map.Keys.OrderBy(k => k).ToList();
    public static string Get(string? name) => name is not null && Map.TryGetValue(name, out var g) ? g : Map["star"];
}

public static class UiSettings
{
    public static (int Port, bool Lan) ReadNetwork(string dataDir)
    {
        var s = new SettingsStore(new Database(Path.Combine(dataDir, "taveloom.db")));
        var port = int.TryParse(s.Get(Keys.Port), out var p) && p is >= 1024 and <= 65535 ? p : 47821;
        return (port, s.GetBool(Keys.Lan, true));
    }
}
