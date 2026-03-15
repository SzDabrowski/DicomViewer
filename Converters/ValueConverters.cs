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


// ── Bool → Tooltip delay (true = normal 400ms, false = effectively disabled) ─────────────────
public class BoolToTooltipDelayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 400 : int.MaxValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToStringConverter : IValueConverter
{
    public string TrueValue { get; set; } = "";
    public string FalseValue { get; set; } = "";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueValue : FalseValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── Bool → White/Muted (for toggle icons like loop) ─────────────────────────
public class BoolToWhiteConverter : IValueConverter
{
    public static readonly BoolToWhiteConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Colors.White)
            : new SolidColorBrush(Color.FromRgb(85, 85, 112)); // TextMuted

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── NotificationSeverity → Background Color ───────────────────────────────────────────
public class SeverityToBackgroundConverter : IValueConverter
{
    public static readonly SeverityToBackgroundConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DicomViewer.ViewModels.NotificationSeverity severity ? severity switch
        {
            DicomViewer.ViewModels.NotificationSeverity.Info    => new SolidColorBrush(Color.FromArgb(230, 16, 30, 50)),
            DicomViewer.ViewModels.NotificationSeverity.Warning => new SolidColorBrush(Color.FromArgb(230, 50, 40, 10)),
            DicomViewer.ViewModels.NotificationSeverity.Error   => new SolidColorBrush(Color.FromArgb(230, 50, 16, 24)),
            _ => new SolidColorBrush(Color.FromArgb(230, 16, 30, 50)),
        } : new SolidColorBrush(Color.FromArgb(230, 16, 30, 50));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── NotificationSeverity → Border Color ───────────────────────────────────────────────
public class SeverityToBorderConverter : IValueConverter
{
    public static readonly SeverityToBorderConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DicomViewer.ViewModels.NotificationSeverity severity ? severity switch
        {
            DicomViewer.ViewModels.NotificationSeverity.Info    => new SolidColorBrush(Color.FromRgb(74, 158, 255)),
            DicomViewer.ViewModels.NotificationSeverity.Warning => new SolidColorBrush(Color.FromRgb(255, 180, 50)),
            DicomViewer.ViewModels.NotificationSeverity.Error   => new SolidColorBrush(Color.FromRgb(255, 74, 106)),
            _ => new SolidColorBrush(Color.FromRgb(74, 158, 255)),
        } : new SolidColorBrush(Color.FromRgb(74, 158, 255));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── NotificationSeverity → Icon Color ─────────────────────────────────────────────────
public class SeverityToIconColorConverter : IValueConverter
{
    public static readonly SeverityToIconColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DicomViewer.ViewModels.NotificationSeverity severity ? severity switch
        {
            DicomViewer.ViewModels.NotificationSeverity.Info    => new SolidColorBrush(Color.FromRgb(74, 158, 255)),
            DicomViewer.ViewModels.NotificationSeverity.Warning => new SolidColorBrush(Color.FromRgb(255, 180, 50)),
            DicomViewer.ViewModels.NotificationSeverity.Error   => new SolidColorBrush(Color.FromRgb(255, 74, 106)),
            _ => new SolidColorBrush(Color.FromRgb(74, 158, 255)),
        } : new SolidColorBrush(Color.FromRgb(74, 158, 255));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── NotificationSeverity → Codicon Kind Name ──────────────────────────────────────────
public class SeverityToIconConverter : IValueConverter
{
    public static readonly SeverityToIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DicomViewer.ViewModels.NotificationSeverity severity ? severity switch
        {
            DicomViewer.ViewModels.NotificationSeverity.Info    => IconPacks.Avalonia.Codicons.PackIconCodiconsKind.Info,
            DicomViewer.ViewModels.NotificationSeverity.Warning => IconPacks.Avalonia.Codicons.PackIconCodiconsKind.Warning,
            DicomViewer.ViewModels.NotificationSeverity.Error   => IconPacks.Avalonia.Codicons.PackIconCodiconsKind.Error,
            _ => IconPacks.Avalonia.Codicons.PackIconCodiconsKind.Info,
        } : IconPacks.Avalonia.Codicons.PackIconCodiconsKind.Info;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── Has Details → Visibility ──────────────────────────────────────────────────────────
public class HasDetailsConverter : IValueConverter
{
    public static readonly HasDetailsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ── LogLevel → Foreground Color ───────────────────────────────────────────────
public class LogLevelToColorConverter : IValueConverter
{
    public static readonly LogLevelToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DicomViewer.Services.LogLevel level ? level switch
        {
            DicomViewer.Services.LogLevel.Debug   => new SolidColorBrush(Color.FromRgb(100, 100, 130)),
            DicomViewer.Services.LogLevel.Info    => new SolidColorBrush(Color.FromRgb(74, 158, 255)),
            DicomViewer.Services.LogLevel.Warning => new SolidColorBrush(Color.FromRgb(255, 180, 50)),
            DicomViewer.Services.LogLevel.Error   => new SolidColorBrush(Color.FromRgb(255, 74, 106)),
            _ => new SolidColorBrush(Color.FromRgb(136, 136, 168)),
        } : new SolidColorBrush(Color.FromRgb(136, 136, 168));

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
