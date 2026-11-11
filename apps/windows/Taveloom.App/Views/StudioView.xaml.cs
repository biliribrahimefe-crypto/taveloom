using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Taveloom.App.Services;
using Taveloom.App.ViewModels;
using Taveloom.Protocol;

namespace Taveloom.App.Views;

/// <summary>Draws the workspace grid exactly as a phone lays it out (same columns, rows and spans) and handles drag-and-drop editing.</summary>
public partial class StudioView : UserControl
{
    private const double Cell = 84;
    private const string ControlFormat = "taveloom/control", NewFormat = "taveloom/new";
    private StudioViewModel? _vm;
    private Point _dragStart;

    public StudioView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
        Unloaded += (_, _) => { if (_vm is not null) _vm.LayoutChanged -= Rebuild; };
    }

    private void Attach()
    {
        if (_vm is not null) _vm.LayoutChanged -= Rebuild;
        _vm = DataContext as StudioViewModel;
        if (_vm is null) return;
        _vm.LayoutChanged += Rebuild;
        Rebuild();
    }

    private Brush Res(string key) => (Brush)FindResource(key);

    private void Rebuild()
    {
        if (_vm is null) return;
        GridHost.Children.Clear(); GridHost.RowDefinitions.Clear(); GridHost.ColumnDefinitions.Clear();
        GridHost.Width = _vm.Columns * Cell; GridHost.Height = _vm.Rows * Cell;
        for (var c = 0; c < _vm.Columns; c++) GridHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Cell) });
        for (var r = 0; r < _vm.Rows; r++) GridHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Cell) });

        for (var r = 0; r < _vm.Rows; r++)
            for (var c = 0; c < _vm.Columns; c++)
            {
                var slot = new Rectangle
                {
                    RadiusX = 14,
                    RadiusY = 14,
                    Margin = new Thickness(4),
                    Fill = Brushes.Transparent,
                    Stroke = Res("Border"),
                    StrokeThickness = 1,
                    StrokeDashArray = [3, 3],
                    AllowDrop = true,
                    Tag = (c, r),
                };
                slot.DragOver += (_, e) => e.Effects = e.Data.GetDataPresent(ControlFormat) || e.Data.GetDataPresent(NewFormat) ? DragDropEffects.Move : DragDropEffects.None;
                slot.Drop += OnSlotDrop;
                Grid.SetColumn(slot, c); Grid.SetRow(slot, r);
                GridHost.Children.Add(slot);
            }

        foreach (var c in _vm.Controls)
        {
            var tile = BuildTile(c);
            Grid.SetColumn(tile, c.Col); Grid.SetRow(tile, c.Row);
            Grid.SetColumnSpan(tile, Math.Max(1, c.ColSpan)); Grid.SetRowSpan(tile, Math.Max(1, c.RowSpan));
            GridHost.Children.Add(tile);
        }
    }

    private Border BuildTile(ControlVM c)
    {
        var selected = ReferenceEquals(_vm?.Selected, c);
        var problem = _vm?.ProblemWith(c);
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = IconGlyphs.Get(c.Icon),
            FontFamily = (FontFamily)FindResource("IconFont"),
            FontSize = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = selected ? Res("Accent") : Res("Text"),
        });
        stack.Children.Add(new TextBlock
        {
            Text = c.Label,
            FontSize = 11.5,
            Margin = new Thickness(6, 6, 6, 0),
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Res("Text"),
            TextWrapping = TextWrapping.NoWrap,
        });
        stack.Children.Add(new TextBlock
        {
            Text = c.Widget switch { WidgetType.Value => $"live {c.Source}", WidgetType.Volume => "slider", WidgetType.Toggle => "toggle", _ => "" },
            FontSize = 10,
            Foreground = Res("TextMuted"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = c.Widget is WidgetType.Button or WidgetType.Media ? Visibility.Collapsed : Visibility.Visible,
        });

        var grid = new Grid(); grid.Children.Add(stack);
        if (problem is not null)
            grid.Children.Add(new TextBlock
            {
                Text = "",
                FontFamily = (FontFamily)FindResource("IconFont"),
                FontSize = 12,
                Foreground = Res("Warning"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 9, 0),
            });

        var tile = new Border
        {
            Margin = new Thickness(4),
            CornerRadius = new CornerRadius(14),
            Child = grid,
            Cursor = Cursors.Hand,
            Tag = c,
            Background = selected ? Res("AccentSoft") : Res("SurfaceAlt"),
            BorderBrush = selected ? Res("Accent") : Res("Border"),
            BorderThickness = new Thickness(selected ? 1.5 : 1),
            ToolTip = problem is null ? c.Label : $"{c.Label}: {problem} (shown as unavailable on the phone)",
        };
        tile.MouseLeftButtonDown += (_, e) => { _dragStart = e.GetPosition(this); _vm!.Selected = c; };
        tile.MouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var d = e.GetPosition(this) - _dragStart;
            if (Math.Abs(d.X) + Math.Abs(d.Y) < 8) return;
            DragDrop.DoDragDrop(tile, new DataObject(ControlFormat, c.Id), DragDropEffects.Move);
        };
        return tile;
    }

    private void OnSlotDrop(object sender, DragEventArgs e)
    {
        if (_vm is null || (sender as FrameworkElement)?.Tag is not ValueTuple<int, int> pos) return;
        var (col, row) = pos;
        if (e.Data.GetData(ControlFormat) is string id && _vm.Controls.FirstOrDefault(x => x.Id == id) is { } c)
        {
            if (!_vm.MoveControl(c, col, row)) _vm.Message = "That spot is taken or the tile does not fit there.";
        }
        else if (e.Data.GetData(NewFormat) is string type) _vm.AddControl(Enum.Parse<WidgetType>(type), col, row);
    }

    private void OnPaletteClick(object sender, RoutedEventArgs e) => _vm?.AddControl(Enum.Parse<WidgetType>((string)((FrameworkElement)sender).Tag));

    private void OnPaletteDrag(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var b = (FrameworkElement)sender;
        DragDrop.DoDragDrop(b, new DataObject(NewFormat, (string)b.Tag), DragDropEffects.Move);
    }
}
