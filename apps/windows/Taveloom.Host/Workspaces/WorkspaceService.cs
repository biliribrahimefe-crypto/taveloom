using System.Text.Json;
using Taveloom.Host.Actions;
using Taveloom.Host.Storage;
using Taveloom.Protocol;

namespace Taveloom.Host.Workspaces;

public sealed class WorkspaceStore(Database db)
{
    public List<Workspace> Load() =>
        db.Query("SELECT json FROM workspaces ORDER BY sort, id",
            r => JsonSerializer.Deserialize<Workspace>(r.GetString(0), ProtocolJson.Options)!);

    public void Save(Workspace w, int sort) =>
        db.Run("INSERT INTO workspaces(id,sort,json) VALUES($i,$s,$j) ON CONFLICT(id) DO UPDATE SET sort=$s, json=$j",
            ("$i", w.Id), ("$s", sort), ("$j", JsonSerializer.Serialize(w, ProtocolJson.Options)));

    public void Delete(string id) => db.Run("DELETE FROM workspaces WHERE id=$i", ("$i", id));
}

/// <summary>
/// Owns workspaces and the authoritative "what is shown on the phone" state.
/// Every client-visible change bumps <see cref="Revision"/>; clients ignore anything not newer than what they hold.
/// </summary>
public sealed class WorkspaceService
{
    private const string KeyActive = "workspace.active", KeyAuto = "workspace.auto", KeySeeded = "workspace.seeded";
    private readonly object _gate = new();
    private readonly WorkspaceStore _store;
    private readonly SettingsStore _settings;
    private readonly ActionEngine _actions;
    private List<Workspace> _all;
    private string? _activeId;
    private bool _auto, _override;
    private string _reason = "Startup";
    private long _revision = 1;

    /// <summary>Raised (outside the lock) after any client-visible change.</summary>
    public event Action? StateChanged;
    /// <summary>Raised when automatic switching is turned on or an override is cleared, so the engine can re-evaluate immediately.</summary>
    public event Action? AutoResumed;

    public WorkspaceService(WorkspaceStore store, SettingsStore settings, ActionEngine actions)
    {
        _store = store; _settings = settings; _actions = actions;
        if (!settings.GetBool(KeySeeded, false))
        {
            var i = 0;
            foreach (var w in BuiltInWorkspaces.Create()) store.Save(w, i++);
            settings.SetBool(KeySeeded, true);
        }
        _all = store.Load();
        _auto = settings.GetBool(KeyAuto, true);
        var saved = settings.Get(KeyActive);
        _activeId = _all.Any(w => w.Id == saved) ? saved : (_all.FirstOrDefault(w => w.IsDefault) ?? _all.FirstOrDefault())?.Id;
    }

    public long Revision { get { lock (_gate) return _revision; } }
    public bool AutoEnabled { get { lock (_gate) return _auto; } }
    public bool IsOverridden { get { lock (_gate) return _override; } }
    public string Reason { get { lock (_gate) return _reason; } }
    public string? ActiveId { get { lock (_gate) return _activeId; } }
    public Workspace? Active { get { lock (_gate) return _all.FirstOrDefault(w => w.Id == _activeId); } }
    public IReadOnlyList<Workspace> All { get { lock (_gate) return _all.ToList(); } }
    public Workspace? Get(string id) { lock (_gate) return _all.FirstOrDefault(w => w.Id == id); }

    public static string NewId() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>Validates and saves (insert or update). Returns the problems; empty means saved.</summary>
    public List<string> Save(Workspace w)
    {
        var errors = WorkspaceValidator.Validate(w, _actions.Validate);
        if (errors.Count > 0) return errors;
        lock (_gate)
        {
            var idx = _all.FindIndex(x => x.Id == w.Id);
            // A single default workspace: marking one default clears the flag elsewhere.
            if (w.IsDefault) _all = _all.Select(x => x.IsDefault && x.Id != w.Id ? x with { IsDefault = false } : x).ToList();
            if (idx >= 0) _all[_all.FindIndex(x => x.Id == w.Id)] = w; else _all.Add(w);
            Persist();
            _activeId ??= w.Id;
            _revision++;
        }
        StateChanged?.Invoke();
        return errors;
    }

    public Workspace Create(string name)
    {
        var w = new Workspace(NewId(), name, "star", 4, 3, false, [], []);
        var errors = Save(w);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
        return w;
    }

    public Workspace? Duplicate(string id)
    {
        var src = Get(id);
        if (src is null) return null;
        var name = src.Name.Length > 33 ? src.Name[..33] : src.Name;
        var copy = src with
        {
            Id = NewId(),
            Name = name + " copy",
            IsDefault = false,
            Rules = src.Rules.Select(r => r with { Id = NewId() }).ToList(),
            Controls = src.Controls.Select(c => c with { Id = NewId() }).ToList(),
        };
        return Save(copy).Count == 0 ? copy : null;
    }

    public bool Delete(string id)
    {
        lock (_gate)
        {
            if (_all.RemoveAll(w => w.Id == id) == 0) return false;
            _store.Delete(id);
            if (_activeId == id) { _activeId = (_all.FirstOrDefault(w => w.IsDefault) ?? _all.FirstOrDefault())?.Id; _reason = "Active workspace deleted"; }
            Persist();
            _revision++;
        }
        StateChanged?.Invoke();
        return true;
    }

    /// <summary>User picked a workspace. While auto-switching is on this becomes an override that holds until auto is resumed.</summary>
    public bool SelectManual(string id)
    {
        lock (_gate)
        {
            if (_all.All(w => w.Id != id)) return false;
            if (_activeId == id && _override == _auto) return true;
            _activeId = id; _override = _auto; _reason = "Manual selection";
            Persist(); _revision++;
        }
        StateChanged?.Invoke();
        return true;
    }

    public void SetAuto(bool enabled)
    {
        lock (_gate)
        {
            if (_auto == enabled && !_override) return;
            _auto = enabled; _override = false;
            _reason = enabled ? "Automatic switching on" : "Automatic switching off";
            Persist(); _revision++;
        }
        StateChanged?.Invoke();
        if (enabled) AutoResumed?.Invoke();
    }

    /// <summary>Called by the adaptive engine. Ignored while auto is off or overridden.</summary>
    public bool ApplyAutomatic(string id, string reason)
    {
        lock (_gate)
        {
            if (!_auto || _override || _all.All(w => w.Id != id)) return false;
            if (_activeId == id) { if (_reason == reason) return false; _reason = reason; _revision++; }
            else { _activeId = id; _reason = reason; _revision++; }
            Persist();
        }
        StateChanged?.Invoke();
        return true;
    }

    public WorkspaceStatePayload BuildState()
    {
        lock (_gate)
        {
            var active = _all.FirstOrDefault(w => w.Id == _activeId);
            return new WorkspaceStatePayload(_revision, _auto && !_override ? "auto" : "manual", _reason,
                active is null ? null : ToClient(active),
                _all.Select(w => new WorkspaceSummary(w.Id, w.Name, w.Icon)).ToList());
        }
    }

    private ClientWorkspace ToClient(Workspace w) => new(w.Id, w.Name, w.Icon, w.Columns, w.Rows,
        w.Controls.Select(c =>
        {
            var a = _actions.Check(c);
            return new ClientControl(c.Id, c.Label, c.Icon, c.Widget, c.Col, c.Row, c.ColSpan, c.RowSpan, c.Source, a.Available, a.Reason);
        }).ToList());

    private void Persist()
    {
        for (var i = 0; i < _all.Count; i++) _store.Save(_all[i], i);
        if (_activeId is not null) _settings.Set(KeyActive, _activeId);
        _settings.SetBool(KeyAuto, _auto);
    }
}
