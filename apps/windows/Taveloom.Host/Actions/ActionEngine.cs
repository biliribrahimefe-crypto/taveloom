using Taveloom.Host.Workspaces;
using Taveloom.Protocol;

namespace Taveloom.Host.Actions;

public sealed record ActionOutcome(bool Ok, string Message, string? ErrorCode = null)
{
    public static ActionOutcome Success(string message) => new(true, message);
    public static ActionOutcome Fail(string code, string message) => new(false, message, code);
}

public sealed record Availability(bool Available, string? Reason = null)
{
    public static readonly Availability Yes = new(true);
    public static Availability No(string reason) => new(false, reason);
}

public interface IActionHandler
{
    ActionType Type { get; }
    /// <summary>Static configuration check. Returns an error message, or null when the spec is well-formed.</summary>
    string? Validate(ActionSpec spec);
    /// <summary>Runtime check (does the file exist, is an audio device present...).</summary>
    Availability Check(ActionSpec spec);
    Task<ActionOutcome> ExecuteAsync(ActionSpec spec, double? value, CancellationToken ct);
}

public sealed class ActionEngine(IEnumerable<IActionHandler> handlers)
{
    public static readonly string[] ValueSources = ["cpu", "ram", "volume", "mic"];
    private readonly Dictionary<ActionType, IActionHandler> _handlers = handlers.ToDictionary(h => h.Type);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    public string? Validate(ControlDef c)
    {
        if (c.Widget == WidgetType.Value)
            return c.Source is not null && ValueSources.Contains(c.Source) ? null : "Value displays need a source: cpu, ram, volume or mic.";
        if (c.Action is null or { Type: ActionType.None })
            return c.Widget == WidgetType.Value ? null : "This control has no action configured.";
        return _handlers.TryGetValue(c.Action.Type, out var h) ? h.Validate(c.Action) : $"Unsupported action type {c.Action.Type}.";
    }

    public Availability Check(ControlDef c)
    {
        if (c.Widget == WidgetType.Value) return ValueSources.Contains(c.Source ?? "") ? Availability.Yes : Availability.No("Unknown value source.");
        if (c.Action is null or { Type: ActionType.None }) return Availability.No("No action configured. Edit this control in Workspace Studio.");
        if (!_handlers.TryGetValue(c.Action.Type, out var h)) return Availability.No("Unsupported action type.");
        return h.Validate(c.Action) is { } err ? Availability.No(err) : h.Check(c.Action);
    }

    public async Task<ActionOutcome> ExecuteAsync(ControlDef c, double? value = null, CancellationToken ct = default)
    {
        var avail = Check(c);
        if (!avail.Available) return ActionOutcome.Fail(ErrorCodes.ActionUnavailable, avail.Reason ?? "Unavailable.");
        if (c.Action is null || !_handlers.TryGetValue(c.Action.Type, out var h))
            return ActionOutcome.Fail(ErrorCodes.ActionUnavailable, "This control has no executable action.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        try
        {
            // Handlers run off the caller's thread so a slow native call can never block the UI or the socket loop.
            return await Task.Run(() => h.ExecuteAsync(c.Action, value, cts.Token), cts.Token).WaitAsync(Timeout, ct);
        }
        catch (TimeoutException) { return ActionOutcome.Fail(ErrorCodes.Timeout, "The action timed out."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return ActionOutcome.Fail(ErrorCodes.Timeout, "The action timed out."); }
        catch (OperationCanceledException) { return ActionOutcome.Fail(ErrorCodes.ActionFailed, "The action was cancelled."); }
        catch (Exception ex) { return ActionOutcome.Fail(ErrorCodes.ActionFailed, ex.Message); }
    }
}
