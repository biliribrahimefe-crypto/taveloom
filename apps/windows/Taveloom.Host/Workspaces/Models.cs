using Taveloom.Host.Actions;
using Taveloom.Protocol;

namespace Taveloom.Host.Workspaces;

/// <summary>A locally configured action. Android never sees <see cref="Args"/> (paths, URLs, keys); it only sends control ids.</summary>
public sealed record ActionSpec(ActionType Type, Dictionary<string, string>? Args = null)
{
    public string? Arg(string key) => Args is not null && Args.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
}

public enum ActionType { None, Hotkey, LaunchApp, LaunchFolder, LaunchUrl, Media, System }

public sealed record ControlDef(string Id, string Label, string Icon, WidgetType Widget,
    int Col, int Row, int ColSpan = 1, int RowSpan = 1, ActionSpec? Action = null, string? Source = null);

/// <summary>Activates a workspace when the foreground process matches. ProcessName is compared case-insensitively, with or without ".exe".</summary>
public sealed record WorkspaceRule(string Id, string ProcessName, string? TitleContains = null, int Priority = 0);

public sealed record Workspace(string Id, string Name, string Icon, int Columns, int Rows, bool IsDefault,
    List<ControlDef> Controls, List<WorkspaceRule> Rules);

public sealed record ForegroundInfo(string ProcessName, string Title);
