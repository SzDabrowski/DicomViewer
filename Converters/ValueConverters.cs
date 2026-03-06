using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DicomViewer.ViewModels;
using System;
using System.Globalization;

namespace DicomViewer.Converters;

// ── Bool → Opacity ──────────────────────────────────────────────────────────────────────────────
public class BoolToOpacityConverter : IValueConverter
{
    public double TrueValue { get; set; } = 1.0;
    public double FalseValue { get; set; } = 0.0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueValue : FalseValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── Bool → Width ───────────────────────────────────────────────────────────────────────────────
public class BoolToWidthConverter : IValueConverter
{
    public double TrueWidth { get; set; } = 260;
    public double FalseWidth { get; set; } = 0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueWidth : FalseWidth;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── Bool → Color (thumbnail border) ──────────────────────────────────────────────────────────
public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Color.FromRgb(74, 158, 255) : Color.FromArgb(0, 0, 0, 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── Bool → Card background color ───────────────────────────────────────────────────────────────
public class BoolToCardColorConverter : IValueConverter
{
    public static readonly BoolToCardColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Color.FromRgb(26, 42, 74) : Color.FromArgb(0, 0, 0, 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── Bool → Play Icon ──────────────────────────────────────────────────────────────────────────────
public class BoolToPlayIconConverter : IValueConverter
{
    public static readonly BoolToPlayIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "⏸" : "▶";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── Bool → Highlight background ───────────────────────────────────────────────────────────────────
public class BoolToHighlightConverter : IValueConverter
{
    public static readonly BoolToHighlightConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.FromArgb(60, 74, 158, 255))
            : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── Bool → Accent brush ────────────────────────────────────────────────────────────────────────────────
public class BoolToAccentConverter : IValueConverter
{
    public static readonly BoolToAccentConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.FromRgb(74, 158, 255))
            : new SolidColorBrush(Color.FromRgb(136, 136, 168));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── TotalFrames → Slider Max ──────────────────────────────────────────────────────────────────────────
public class TotalFramesToMaxConverter : IValueConverter
{
    public static readonly TotalFramesToMaxConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int total ? Math.Max(0, total - 1) : 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── Tool → Active Bool ─────────────────────────────────────────────────────────────────────────────
public class ToolToActiveConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MouseTool active && parameter is string toolName)
            return active.ToString().Equals(toolName, StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// Progress (0-100) to pixel width (max 280px)
public class ProgressToWidthConverter : IValueConverter
{
    public static readonly ProgressToWidthConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double d ? Math.Max(0, d / 100.0 * 280.0) : 0.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}


public class BoolToStringConverter : IValueConverter
{
    public string TrueValue { get; set; } = "▲";
    public string FalseValue { get; set; } = "▼";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueValue : FalseValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// MouseTool → accent blue when a tool is active, muted grey when None
public class ActiveToolColorConverter : IValueConverter
{
    public static readonly ActiveToolColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is MouseTool tool && tool != MouseTool.None
            ? new SolidColorBrush(Color.FromRgb(74, 158, 255))   // blue
            : new SolidColorBrush(Color.FromRgb(136, 136, 168)); // muted

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
