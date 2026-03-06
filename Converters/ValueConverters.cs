using Avalonia.Data.Converters;
using Avalonia.Media;
using DicomViewer.ViewModels;
using System;
using System.Globalization;

namespace DicomViewer.Converters;

public class BoolToOpacityConverter : IValueConverter
{
    public double TrueValue { get; set; } = 1.0;
    public double FalseValue { get; set; } = 0.0;
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is true ? TrueValue : FalseValue;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}
public class BoolToWidthConverter : IValueConverter
{
    public double TrueWidth { get; set; } = 260;
    public double FalseWidth { get; set; } = 0;
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is true ? TrueWidth : FalseWidth;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}
public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is true ? Color.FromRgb(74, 158, 255) : Color.FromArgb(0, 0, 0, 0);
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}
public class BoolToAccentConverter : IValueConverter
{
    public static readonly BoolToAccentConverter Instance = new();
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is true ? new SolidColorBrush(Color.FromRgb(74, 158, 255)) : new SolidColorBrush(Color.FromRgb(136, 136, 168));
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}
public class BoolToHighlightConverter : IValueConverter
{
    public static readonly BoolToHighlightConverter Instance = new();
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is true ? new SolidColorBrush(Color.FromArgb(60, 74, 158, 255)) : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}
public class BoolToPlayIconConverter : IValueConverter
{
    public static readonly BoolToPlayIconConverter Instance = new();
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is true ? "pause" : "play";
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}
public class TotalFramesToMaxConverter : IValueConverter
{
    public static readonly TotalFramesToMaxConverter Instance = new();
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is int total ? Math.Max(0, total - 1) : 0;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}
public class ToolToActiveConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is MouseTool active && p is string name && active.ToString().Equals(name, StringComparison.OrdinalIgnoreCase);
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}
public class ProgressToWidthConverter : IValueConverter
{
    public static readonly ProgressToWidthConverter Instance = new();
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is double d ? Math.Max(0, d / 100.0 * 280.0) : 0.0;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}
public class BoolToStringConverter : IValueConverter
{
    public string TrueValue { get; set; } = "▲";
    public string FalseValue { get; set; } = "▼";
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is true ? TrueValue : FalseValue;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}