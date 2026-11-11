namespace Taveloom.Host.Workspaces;

public sealed record RuleMatch(Workspace? Workspace, string Reason, WorkspaceRule? Rule);

/// <summary>Chooses a workspace for the foreground window. Pure and deterministic.</summary>
public static class WorkspaceResolver
{
    public static string NormalizeProcess(string name)
    {
        name = name.Trim().ToLowerInvariant();
        return name.EndsWith(".exe", StringComparison.Ordinal) ? name[..^4] : name;
    }

    public static bool Matches(WorkspaceRule r, ForegroundInfo fg) =>
        NormalizeProcess(r.ProcessName) == NormalizeProcess(fg.ProcessName) &&
        (string.IsNullOrWhiteSpace(r.TitleContains) || fg.Title.Contains(r.TitleContains, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Order of precedence: highest rule priority, then rules that also match a window title (more specific),
    /// then workspace order. If nothing matches, the default workspace is used; if there is none, no change (null).
    /// </summary>
    public static RuleMatch Resolve(IReadOnlyList<Workspace> workspaces, ForegroundInfo? fg)
    {
        if (fg is not null)
        {
            var best = workspaces.SelectMany((w, i) => w.Rules.Select(r => (w, r, i)))
                .Where(x => Matches(x.r, fg))
                .OrderByDescending(x => x.r.Priority)
                .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.r.TitleContains))
                .ThenBy(x => x.i)
                .FirstOrDefault();
            if (best.w is not null) return new RuleMatch(best.w, $"Rule: {best.r.ProcessName}", best.r);
        }
        var def = workspaces.FirstOrDefault(w => w.IsDefault);
        return def is null ? new RuleMatch(null, "No matching rule", null) : new RuleMatch(def, "Default workspace", null);
    }
}

public static class WorkspaceValidator
{
    /// <summary>Returns human-readable problems; empty means the workspace is safe to save.</summary>
    public static List<string> Validate(Workspace w, Func<ControlDef, string?> validateControl)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(w.Name) || w.Name.Length > 40) errors.Add("Name must be 1-40 characters.");
        if (w.Columns is < 1 or > 8) errors.Add("Columns must be between 1 and 8.");
        if (w.Rows is < 1 or > 12) errors.Add("Rows must be between 1 and 12.");
        if (w.Controls.Select(c => c.Id).Distinct().Count() != w.Controls.Count) errors.Add("Control ids must be unique.");

        var occupied = new HashSet<(int, int)>();
        foreach (var c in w.Controls)
        {
            if (string.IsNullOrWhiteSpace(c.Label) || c.Label.Length > 30) errors.Add($"Control '{c.Id}': label must be 1-30 characters.");
            if (c.ColSpan < 1 || c.RowSpan < 1 || c.Col < 0 || c.Row < 0 || c.Col + c.ColSpan > w.Columns || c.Row + c.RowSpan > w.Rows)
            {
                errors.Add($"Control '{c.Label}' does not fit in the grid."); continue;
            }
            for (var x = c.Col; x < c.Col + c.ColSpan; x++)
                for (var y = c.Row; y < c.Row + c.RowSpan; y++)
                    if (!occupied.Add((x, y))) { errors.Add($"Control '{c.Label}' overlaps another control."); x = int.MaxValue - 1; break; }
            if (c.Action is { Type: not ActionType.None } && validateControl(c) is { } err) errors.Add($"Control '{c.Label}': {err}");
        }
        foreach (var r in w.Rules)
            if (string.IsNullOrWhiteSpace(r.ProcessName)) errors.Add("Rules need a process name.");
        return errors;
    }
}
