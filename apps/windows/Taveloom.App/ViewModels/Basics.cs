using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Taveloom.App.Services;

namespace Taveloom.App.ViewModels;

/// <summary>Marshals host-thread events onto the UI thread.</summary>
public static class Ui
{
    public static void Post(Action a)
    {
        var d = Application.Current?.Dispatcher;
        if (d is null) return;
        if (d.CheckAccess()) a(); else d.BeginInvoke(a);
    }
}

public sealed class IconGlyphConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => IconGlyphs.Get(v as string);
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
    {
        var on = v is true; if (p as string == "invert") on = !on;
        return on ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
    {
        var has = v is not null && !(v is string s && s.Length == 0); if (p as string == "invert") has = !has;
        return has ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Maps a status keyword (ok/warn/bad/idle) to a theme brush.</summary>
public sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        Application.Current.TryFindResource((v as string) switch { "ok" => "Success", "warn" => "Warning", "bad" => "Danger", _ => "TextMuted" }) as Brush ?? Brushes.Gray;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => Equals(v?.ToString(), p?.ToString());
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
