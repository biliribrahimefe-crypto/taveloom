using Taveloom.Protocol;

namespace Taveloom.Host.Workspaces;

/// <summary>Editable starter templates. Nothing is assumed to be installed: paths are filled only if found, otherwise the control reports "not configured".</summary>
public static class BuiltInWorkspaces
{
    public static List<Workspace> Create()
    {
        string Id() => WorkspaceService.NewId();
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);

        string? FirstExisting(params string[] paths) => paths.FirstOrDefault(File.Exists);
        ActionSpec App(string? path) => new(ActionType.LaunchApp, path is null ? null : new() { ["path"] = path });
        ActionSpec Url(string url) => new(ActionType.LaunchUrl, new() { ["url"] = url });
        ActionSpec Folder(string? p) => new(ActionType.LaunchFolder, p is null ? null : new() { ["path"] = p });
        ActionSpec Media(string cmd) => new(ActionType.Media, new() { ["command"] = cmd });
        ActionSpec Sys(string cmd) => new(ActionType.System, new() { ["command"] = cmd });

        var code = FirstExisting(Path.Combine(local, "Programs", "Microsoft VS Code", "Code.exe"),
            @"C:\Program Files\Microsoft VS Code\Code.exe");
        var terminal = FirstExisting(Path.Combine(local, "Microsoft", "WindowsApps", "wt.exe"),
            Path.Combine(sys, "WindowsPowerShell", "v1.0", "powershell.exe"));
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        var dev = new Workspace(Id(), "Development", "code", 4, 3, false,
        [
            new(Id(), "VS Code", "code", WidgetType.Button, 0, 0, Action: App(code)),
            new(Id(), "Terminal", "terminal", WidgetType.Button, 1, 0, Action: App(terminal)),
            new(Id(), "Project", "folder", WidgetType.Button, 2, 0, Action: Folder(null)),
            new(Id(), "GitHub", "globe", WidgetType.Button, 3, 0, Action: Url("https://github.com")),
            new(Id(), "Screenshot", "camera", WidgetType.Button, 0, 1, Action: Sys("screenshot")),
            new(Id(), "Previous", "skip_previous", WidgetType.Media, 1, 1, Action: Media("previous")),
            new(Id(), "Play / Pause", "play_pause", WidgetType.Media, 2, 1, Action: Media("playpause")),
            new(Id(), "Next", "skip_next", WidgetType.Media, 3, 1, Action: Media("next")),
        ],
        [new(Id(), "Code.exe", null, 10)]);

        var prod = new Workspace(Id(), "Productivity", "briefcase", 4, 3, true,
        [
            new(Id(), "Browser", "globe", WidgetType.Button, 0, 0, Action: Url("https://duckduckgo.com")),
            new(Id(), "Documents", "folder", WidgetType.Button, 1, 0, Action: Folder(docs)),
            new(Id(), "Downloads", "download", WidgetType.Button, 2, 0, Action: Folder(downloads)),
            new(Id(), "Notepad", "note", WidgetType.Button, 3, 0, Action: App(Path.Combine(sys, "notepad.exe"))),
            new(Id(), "Volume", "volume", WidgetType.Volume, 0, 1, 2, 1, Media("volume")),
            new(Id(), "Mute", "mute", WidgetType.Toggle, 2, 1, Action: Media("mute"), Source: "mute"),
            new(Id(), "Screenshot", "camera", WidgetType.Button, 3, 1, Action: Sys("screenshot")),
            new(Id(), "Lock PC", "lock", WidgetType.Button, 0, 2, Action: Sys("lock")),
            new(Id(), "CPU", "cpu", WidgetType.Value, 1, 2, Source: "cpu"),
            new(Id(), "Memory", "memory", WidgetType.Value, 2, 2, Source: "ram"),
        ],
        new[] { "chrome.exe", "msedge.exe", "firefox.exe", "WINWORD.EXE", "EXCEL.EXE", "OUTLOOK.EXE" }
            .Select(p => new WorkspaceRule(Id(), p, null, 5)).ToList());

        var media = new Workspace(Id(), "Media", "music", 4, 3, false,
        [
            new(Id(), "Previous", "skip_previous", WidgetType.Media, 0, 0, Action: Media("previous")),
            new(Id(), "Play / Pause", "play_pause", WidgetType.Media, 1, 0, Action: Media("playpause")),
            new(Id(), "Next", "skip_next", WidgetType.Media, 2, 0, Action: Media("next")),
            new(Id(), "Mute", "mute", WidgetType.Toggle, 3, 0, Action: Media("mute"), Source: "mute"),
            new(Id(), "Volume", "volume", WidgetType.Volume, 0, 1, 3, 1, Media("volume")),
            new(Id(), "Level", "volume", WidgetType.Value, 3, 1, Source: "volume"),
        ],
        new[] { "Spotify.exe", "vlc.exe", "wmplayer.exe", "Music.UI.exe", "Microsoft.Media.Player.exe" }
            .Select(p => new WorkspaceRule(Id(), p, null, 5)).ToList());

        return [dev, prod, media];
    }
}
