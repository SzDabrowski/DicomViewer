using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DicomViewer.ViewModels;
using System;
using System.Collections.Generic;

namespace DicomViewer.Controls;

public class DicomViewerControl : Control
{
    public static readonly StyledProperty<Bitmap?> DicomImageProperty = AvaloniaProperty.Register<DicomViewerControl, Bitmap?>(nameof(DicomImage));
    public static readonly StyledProperty<double> ZoomLevelProperty = AvaloniaProperty.Register<DicomViewerControl, double>(nameof(ZoomLevel), 1.0);
    public static readonly StyledProperty<double> PanXProperty = AvaloniaProperty.Register<DicomViewerControl, double>(nameof(PanX), 0.0);
    public static readonly StyledProperty<double> PanYProperty = AvaloniaProperty.Register<DicomViewerControl, double>(nameof(PanY), 0.0);
    public static readonly StyledProperty<double> RotationProperty = AvaloniaProperty.Register<DicomViewerControl, double>(nameof(Rotation), 0.0);
    public static readonly StyledProperty<bool> InvertColorsProperty = AvaloniaProperty.Register<DicomViewerControl, bool>(nameof(InvertColors), false);
    public static readonly StyledProperty<bool> ShowOverlayProperty = AvaloniaProperty.Register<DicomViewerControl, bool>(nameof(ShowOverlay), true);
    public static readonly StyledProperty<MouseTool> ActiveToolProperty = AvaloniaProperty.Register<DicomViewerControl, MouseTool>(nameof(ActiveTool), MouseTool.Pan);
    public static readonly StyledProperty<string> OverlayTextProperty = AvaloniaProperty.Register<DicomViewerControl, string>(nameof(OverlayText), string.Empty);
    public static readonly StyledProperty<double> WindowCenterProperty = AvaloniaProperty.Register<DicomViewerControl, double>(nameof(WindowCenter), 40);
    public static readonly StyledProperty<double> WindowWidthProperty = AvaloniaProperty.Register<DicomViewerControl, double>(nameof(WindowWidth), 400);

    public Bitmap? DicomImage { get => GetValue(DicomImageProperty); set => SetValue(DicomImageProperty, value); }
    public double ZoomLevel { get => GetValue(ZoomLevelProperty); set => SetValue(ZoomLevelProperty, value); }
    public double PanX { get => GetValue(PanXProperty); set => SetValue(PanXProperty, value); }
    public double PanY { get => GetValue(PanYProperty); set => SetValue(PanYProperty, value); }
    public double Rotation { get => GetValue(RotationProperty); set => SetValue(RotationProperty, value); }
    public bool InvertColors { get => GetValue(InvertColorsProperty); set => SetValue(InvertColorsProperty, value); }
    public bool ShowOverlay { get => GetValue(ShowOverlayProperty); set => SetValue(ShowOverlayProperty, value); }
    public MouseTool ActiveTool { get => GetValue(ActiveToolProperty); set => SetValue(ActiveToolProperty, value); }
    public string OverlayText { get => GetValue(OverlayTextProperty); set => SetValue(OverlayTextProperty, value); }
    public double WindowCenter { get => GetValue(WindowCenterProperty); set => SetValue(WindowCenterProperty, value); }
    public double WindowWidth { get => GetValue(WindowWidthProperty); set => SetValue(WindowWidthProperty, value); }

    public event EventHandler<double>? ZoomChanged;
    public event EventHandler<(double X, double Y)>? PanChanged;
    public event EventHandler<(double Center, double Width)>? WindowLevelChanged;

    private bool _isDragging;
    private Point _lastPointerPos;
    private readonly List<(Point Start, Point End)> _measurements = new();
    private Point? _measureStart;
    private static readonly IPen MeasurePen = new Pen(Brushes.Yellow, 1.5);
    private static readonly IPen CrosshairPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1);
    private static readonly Typeface OverlayFont = new(FontFamily.Default);

    static DicomViewerControl()
    {
        AffectsRender<DicomViewerControl>(DicomImageProperty, ZoomLevelProperty, PanXProperty, PanYProperty, RotationProperty, InvertColorsProperty, ShowOverlayProperty, WindowCenterProperty, WindowWidthProperty);
    }

    public DicomViewerControl() { ClipToBounds = true; Focusable = true; Cursor = new Cursor(StandardCursorType.Arrow); }

    public override void Render(DrawingContext ctx)
    {
        var bounds = Bounds;
        ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(10, 10, 14)), new Rect(bounds.Size));
        if (DicomImage == null)
        {
            var text = new FormattedText("Drop DICOM files here or use File -> Open", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, OverlayFont, 14, new SolidColorBrush(Color.FromArgb(100, 200, 200, 220)));
            ctx.DrawText(text, new Point(bounds.Width / 2 - text.Width / 2, bounds.Height / 2 - 10));
            return;
        }
        var cx = bounds.Width / 2 + PanX; var cy = bounds.Height / 2 + PanY;
        using (ctx.PushTransform(Matrix.CreateTranslation(-cx, -cy) * Matrix.CreateRotation(Rotation * Math.PI / 180) * Matrix.CreateScale(ZoomLevel, ZoomLevel) * Matrix.CreateTranslation(cx, cy)))
        {
            var imgW = DicomImage.Size.Width * ZoomLevel; var imgH = DicomImage.Size.Height * ZoomLevel;
            ctx.DrawImage(DicomImage, new Rect(cx - imgW / 2, cy - imgH / 2, imgW, imgH));
        }
        ctx.DrawLine(CrosshairPen, new Point(0, bounds.Height / 2), new Point(bounds.Width, bounds.Height / 2));
        ctx.DrawLine(CrosshairPen, new Point(bounds.Width / 2, 0), new Point(bounds.Width / 2, bounds.Height));
        foreach (var (s, e) in _measurements)
        {
            ctx.DrawLine(MeasurePen, s, e);
            var ft = new FormattedText($"{Math.Sqrt(Math.Pow(e.X - s.X, 2) + Math.Pow(e.Y - s.Y, 2)):F1}px", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, OverlayFont, 11, Brushes.Yellow);
            ctx.DrawText(ft, e);
        }
        if (ShowOverlay)
        {
            var ft = new FormattedText($"C: {WindowCenter:F0}  W: {WindowWidth:F0}\nZoom: {ZoomLevel * 100:F0}%  Rot: {Rotation:F0}deg", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, OverlayFont, 11, Brushes.White);
            ctx.DrawText(ft, new Point(8, 8));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); _isDragging = true; _lastPointerPos = e.GetPosition(this); if (ActiveTool == MouseTool.Measure) _measureStart = _lastPointerPos; e.Pointer.Capture(this); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); if (_isDragging && ActiveTool == MouseTool.Measure && _measureStart.HasValue) { _measurements.Add((_measureStart.Value, e.GetPosition(this))); _measureStart = null; InvalidateVisual(); } _isDragging = false; e.Pointer.Capture(null); }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); if (!_isDragging) return;
        var pos = e.GetPosition(this); var dx = pos.X - _lastPointerPos.X; var dy = pos.Y - _lastPointerPos.Y;
        switch (ActiveTool)
        {
            case MouseTool.Pan: PanX += dx; PanY += dy; PanChanged?.Invoke(this, (PanX, PanY)); break;
            case MouseTool.Zoom: ZoomLevel = Math.Clamp(ZoomLevel * (1.0 + dy * -0.005), 0.05, 20.0); ZoomChanged?.Invoke(this, ZoomLevel); break;
            case MouseTool.WindowLevel: WindowCenter += dx * 2; WindowWidth = Math.Max(1, WindowWidth + dy * 4); WindowLevelChanged?.Invoke(this, (WindowCenter, WindowWidth)); break;
            case MouseTool.Rotate: Rotation = (Rotation + dx * 0.5 + 360) % 360; break;
        }
        _lastPointerPos = pos; InvalidateVisual();
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) { base.OnPointerWheelChanged(e); ZoomLevel = Math.Clamp(ZoomLevel * (e.Delta.Y > 0 ? 1.1 : 0.9), 0.05, 20.0); ZoomChanged?.Invoke(this, ZoomLevel); InvalidateVisual(); }
    public void ClearMeasurements() { _measurements.Clear(); InvalidateVisual(); }
}