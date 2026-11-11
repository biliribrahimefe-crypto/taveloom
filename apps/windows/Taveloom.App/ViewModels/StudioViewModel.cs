using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Taveloom.Host;
using Taveloom.Host.Workspaces;
using Taveloom.Protocol;

namespace Taveloom.App.ViewModels;

public sealed partial class ControlVM : ObservableObject
{
    public string Id { get; init; } = WorkspaceService.NewId();
    [ObservableProperty] private string _label = "New control";
    [ObservableProperty] private string _icon = "star";
    [ObservableProperty] private WidgetType _widget;
    [ObservableProperty] private int _col, _row;
    [ObservableProperty] private int _colSpan = 1, _rowSpan = 1;
    [ObservableProperty] private ActionType _actionType;
    [ObservableProperty] private string _target = "";
    [ObservableProperty] private string _arguments = "";
    [ObservableProperty] private string? _source;

    /// <summary>Argument key that <see cref="Target"/> maps to for each action type.</summary>
    public static string? TargetKey(ActionType t) => t switch
    {
        ActionType.Hotkey => "keys",
        ActionType.LaunchApp or ActionType.LaunchFolder => "path",
        ActionType.LaunchUrl => "url",
        ActionType.Media or ActionType.System => "command",
        _ => null,
    };

    public static ControlVM From(ControlDef c)
    {
        var t = c.Action?.Type ?? ActionType.None;
        return new ControlVM
        {
            Id = c.Id,
            Label = c.Label,
            Icon = c.Icon,
            Widget = c.Widget,
            Col = c.Col,
            Row = c.Row,
            ColSpan = c.ColSpan,
            RowSpan = c.RowSpan,
            ActionType = t,
            Source = c.Source,
            Target = TargetKey(t) is { } k ? c.Action?.Arg(k) ?? "" : "",
            Arguments = c.Action?.Arg("args") ?? "",
        };
    }

    public ControlDef ToDef()
    {
        ActionSpec? spec = null;
        if (ActionType != ActionType.None)
        {
            var args = new Dictionary<string, string>();
            if (TargetKey(ActionType) is { } k && !string.IsNullOrWhiteSpace(Target)) args[k] = Target.Trim();
            if (ActionType == ActionType.LaunchApp && !string.IsNullOrWhiteSpace(Arguments)) args["args"] = Arguments.Trim();
            spec = new ActionSpec(ActionType, args.Count == 0 ? null : args);
        }
        return new ControlDef(Id, Label.Trim(), Icon, Widget, Col, Row, ColSpan, RowSpan, spec, Widget is WidgetType.Value or WidgetType.Toggle ? Source : null);
    }

    public bool IsLaunchApp => ActionType == ActionType.LaunchApp;
    public bool NeedsBrowse => ActionType is ActionType.LaunchApp or ActionType.LaunchFolder;
    public bool NeedsTarget => ActionType != ActionType.None;
    public bool IsValueWidget => Widget == WidgetType.Value;
    public string TargetHint => ActionType switch
    {
        ActionType.Hotkey => "e.g. ctrl+shift+s",
        ActionType.LaunchApp => @"Full path to an .exe or .lnk",
        ActionType.LaunchFolder => "Full folder path",
        ActionType.LaunchUrl => "https://…",
        ActionType.Media => "playpause, next, previous, stop, volumeup, volumedown, mute, volume",
        ActionType.System => "screenshot or lock",
        _ => "",
    };

    partial void OnActionTypeChanged(ActionType value)
    {
        OnPropertyChanged(nameof(IsLaunchApp)); OnPropertyChanged(nameof(NeedsBrowse)); OnPropertyChanged(nameof(NeedsTarget)); OnPropertyChanged(nameof(TargetHint));
    }
    partial void OnWidgetChanged(WidgetType value) => OnPropertyChanged(nameof(IsValueWidget));
}

public sealed partial class RuleVM : ObservableObject
{
    public string Id { get; init; } = WorkspaceService.NewId();
    [ObservableProperty] private string _processName = "";
    [ObservableProperty] private string _titleContains = "";
    [ObservableProperty] private int _priority;
}

public sealed partial class StudioViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private TaveloomHost Host => _main.Host;
    private string? _editingId;
    private bool _loading;

    public ObservableCollection<WorkspaceItem> List { get; } = [];
    public ObservableCollection<ControlVM> Controls { get; } = [];
    public ObservableCollection<RuleVM> Rules { get; } = [];
    public ActionType[] ActionTypes { get; } = Enum.GetValues<ActionType>();
    public WidgetType[] WidgetTypes { get; } = Enum.GetValues<WidgetType>();
    public IReadOnlyList<string> Icons => Services.IconGlyphs.Names;
    public int[] ColumnOptions { get; } = [2, 3, 4, 5, 6];
    public int[] RowOptions { get; } = [2, 3, 4, 5, 6, 8];

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _icon = "star";
    [ObservableProperty] private int _columns = 4, _rows = 3;
    [ObservableProperty] private bool _isDefault;
    [ObservableProperty] private ControlVM? _selected;
    [ObservableProperty] private WorkspaceItem? _selectedItem;
    [ObservableProperty] private bool _hasWorkspace;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private bool _messageIsError;

    /// <summary>The grid view rebuilds itself when this fires.</summary>
    public event Action? LayoutChanged;

    public StudioViewModel(MainViewModel main)
    {
        _main = main;
        Controls.CollectionChanged += (_, _) => Touched(true);
        Rules.CollectionChanged += (_, _) => Touched(false);
        Host.Workspaces.StateChanged += () => Ui.Post(RefreshList);
        RefreshList();
        if (Host.Workspaces.All.FirstOrDefault() is { } first) Load(first.Id);
    }

    private void Touched(bool layout) { if (_loading) return; IsDirty = true; if (layout) LayoutChanged?.Invoke(); }
    partial void OnNameChanged(string value) => Touched(false);
    partial void OnIconChanged(string value) => Touched(false);
    partial void OnIsDefaultChanged(bool value) => Touched(false);
    partial void OnColumnsChanged(int value) => Touched(true);
    partial void OnRowsChanged(int value) => Touched(true);
    partial void OnSelectedChanged(ControlVM? oldValue, ControlVM? newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= OnControlChanged;
        if (newValue is not null) newValue.PropertyChanged += OnControlChanged;
        LayoutChanged?.Invoke();
    }
    private void OnControlChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e) { if (!_loading) { IsDirty = true; LayoutChanged?.Invoke(); } }

    partial void OnSelectedItemChanged(WorkspaceItem? value)
    {
        if (value is not null && value.Id != _editingId) { Load(value.Id); }
    }

    private void RefreshList()
    {
        List.Clear();
        foreach (var w in Host.Workspaces.All) List.Add(new WorkspaceItem(w.Id, w.Name, w.Icon, w.Id == Host.Workspaces.ActiveId));
        var keep = List.FirstOrDefault(i => i.Id == _editingId);
        if (keep is not null && !ReferenceEquals(SelectedItem, keep)) { var was = _editingId; SelectedItem = keep; _editingId = was; }
    }

    public void Load(string id)
    {
        var w = Host.Workspaces.Get(id);
        if (w is null) { HasWorkspace = false; return; }
        _loading = true;
        try
        {
            _editingId = w.Id; Name = w.Name; Icon = w.Icon; Columns = w.Columns; Rows = w.Rows; IsDefault = w.IsDefault;
            Controls.Clear(); foreach (var c in w.Controls) Controls.Add(ControlVM.From(c));
            Rules.Clear(); foreach (var r in w.Rules) Rules.Add(new RuleVM { Id = r.Id, ProcessName = r.ProcessName, TitleContains = r.TitleContains ?? "", Priority = r.Priority });
            Selected = null; HasWorkspace = true; IsDirty = false; Message = null;
            SelectedItem = List.FirstOrDefault(i => i.Id == id);
        }
        finally { _loading = false; }
        LayoutChanged?.Invoke();
    }

    private Workspace Build() => new(_editingId!, Name.Trim(), Icon, Columns, Rows, IsDefault,
        Controls.Select(c => c.ToDef()).ToList(),
        Rules.Where(r => !string.IsNullOrWhiteSpace(r.ProcessName))
             .Select(r => new WorkspaceRule(r.Id, r.ProcessName.Trim(), string.IsNullOrWhiteSpace(r.TitleContains) ? null : r.TitleContains.Trim(), r.Priority)).ToList());

    private void Report(string text, bool error) { Message = text; MessageIsError = error; }

    [RelayCommand]
    private void Save()
    {
        if (_editingId is null) return;
        var errors = Host.Workspaces.Save(Build());
        if (errors.Count > 0) { Report(string.Join("  ", errors), true); return; }
        IsDirty = false; Report("Saved. Connected phones were updated.", false);
    }

    [RelayCommand] private void Revert() { if (_editingId is not null) Load(_editingId); Report("Reverted to the saved version.", false); }

    [RelayCommand]
    private void NewWorkspace()
    {
        var w = Host.Workspaces.Create(UniqueName("New workspace"));
        RefreshList(); Load(w.Id);
    }

    [RelayCommand]
    private void Duplicate()
    {
        if (_editingId is null) return;
        if (Host.Workspaces.Duplicate(_editingId) is { } copy) { RefreshList(); Load(copy.Id); }
    }

    [RelayCommand]
    private void Delete()
    {
        if (_editingId is null) return;
        if (System.Windows.MessageBox.Show($"Delete workspace \"{Name}\"? This cannot be undone.", "Delete workspace",
                System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.OK) return;
        Host.Workspaces.Delete(_editingId);
        _editingId = null; RefreshList();
        if (Host.Workspaces.All.FirstOrDefault() is { } first) Load(first.Id); else { HasWorkspace = false; Controls.Clear(); }
    }

    private string UniqueName(string baseName)
    {
        var names = Host.Workspaces.All.Select(w => w.Name).ToHashSet();
        for (var i = 1; ; i++) { var n = i == 1 ? baseName : $"{baseName} {i}"; if (!names.Contains(n)) return n; }
    }

    /// <summary>Why this control would show as unavailable on the phone, or null if it is ready.</summary>
    public string? ProblemWith(ControlVM c)
    {
        var def = c.ToDef();
        if (Host.Actions.Validate(def) is { } err) return err;
        var a = Host.Actions.Check(def);
        return a.Available ? null : a.Reason;
    }

    // ---- Controls ----
    public bool TryFindFreeCell(int colSpan, int rowSpan, out int col, out int row)
    {
        for (var r = 0; r + rowSpan <= Rows; r++)
            for (var c = 0; c + colSpan <= Columns; c++)
                if (!Overlaps(c, r, colSpan, rowSpan, null)) { col = c; row = r; return true; }
        col = row = 0; return false;
    }

    public bool Overlaps(int col, int row, int cs, int rs, ControlVM? ignore) =>
        Controls.Any(o => o != ignore && col < o.Col + o.ColSpan && o.Col < col + cs && row < o.Row + o.RowSpan && o.Row < row + rs);

    public void AddControl(WidgetType type, int? col = null, int? row = null)
    {
        var span = type == WidgetType.Volume ? Math.Min(2, Columns) : 1;
        int c, r;
        if (col is { } cc && row is { } rr && cc + span <= Columns && !Overlaps(cc, rr, span, 1, null)) { c = cc; r = rr; }
        else if (!TryFindFreeCell(span, 1, out c, out r)) { Report("The grid is full. Increase rows or remove a control.", true); return; }
        var vm = type switch
        {
            WidgetType.Toggle => new ControlVM { Label = "Mute", Icon = "mute", ActionType = ActionType.Media, Target = "mute", Source = "mute" },
            WidgetType.Value => new ControlVM { Label = "CPU", Icon = "cpu", Source = "cpu" },
            WidgetType.Media => new ControlVM { Label = "Play / Pause", Icon = "play_pause", ActionType = ActionType.Media, Target = "playpause" },
            WidgetType.Volume => new ControlVM { Label = "Volume", Icon = "volume", ActionType = ActionType.Media, Target = "volume" },
            _ => new ControlVM { Label = "New button", Icon = "star" },
        };
        vm.Widget = type; vm.Col = c; vm.Row = r; vm.ColSpan = span;
        Controls.Add(vm); Selected = vm;
    }

    [RelayCommand] private void AddButton(string type) => AddControl(Enum.Parse<WidgetType>(type));

    public bool MoveControl(ControlVM c, int col, int row)
    {
        if (col < 0 || row < 0 || col + c.ColSpan > Columns || row + c.RowSpan > Rows || Overlaps(col, row, c.ColSpan, c.RowSpan, c)) return false;
        c.Col = col; c.Row = row; return true;
    }

    [RelayCommand]
    private void RemoveSelected() { if (Selected is { } s) { Controls.Remove(s); Selected = null; } }

    [RelayCommand]
    private void AddRule() => Rules.Add(new RuleVM { Priority = 5 });

    [RelayCommand]
    private void AddRuleFromFocusedApp()
    {
        if (Host.Adaptive.LastForeground is { } fg) Rules.Add(new RuleVM { ProcessName = fg.ProcessName, Priority = 5 });
        else Report("No other application has been focused yet. Switch to the app, then come back.", true);
    }

    [RelayCommand] private void RemoveRule(RuleVM r) => Rules.Remove(r);

    [RelayCommand]
    private async Task TestSelected()
    {
        if (Selected is not { } s) return;
        var def = s.ToDef();
        if (Host.Actions.Validate(def) is { } err) { Report(err, true); return; }
        if (s.Widget == WidgetType.Volume) { Report("Volume sliders are tested from the phone so your volume is not changed unexpectedly.", false); return; }
        var outcome = await Host.Actions.ExecuteAsync(def);
        Report(outcome.Ok ? $"Ran on this PC: {outcome.Message}" : $"Failed: {outcome.Message}", !outcome.Ok);
    }

    [RelayCommand]
    private void Browse()
    {
        if (Selected is not { } s) return;
        if (s.ActionType == ActionType.LaunchFolder)
        {
            var d = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a folder" };
            if (d.ShowDialog() == true) s.Target = d.FolderName;
        }
        else
        {
            var d = new Microsoft.Win32.OpenFileDialog { Title = "Choose an application", Filter = "Applications (*.exe;*.lnk)|*.exe;*.lnk" };
            if (d.ShowDialog() == true) s.Target = d.FileName;
        }
    }
}
