using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DicomViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DicomViewer.Controls;

public class DicomCanvas : Control
{
    // ── Styled Properties ───────────────────────────────────────────────────────────────────────────
    public static readonly StyledProperty<double> WindowWidthProperty =
        AvaloniaProperty.Register<DicomCanvas, double>(nameof(WindowWidth), 65000);

    public static readonly StyledProperty<double> WindowCenterProperty =
        AvaloniaProperty.Register<DicomCanvas, double>(nameof(WindowCenter), 32000);

    public static readonly StyledProperty<bool> IsInvertedProperty =
        AvaloniaProperty.Register<DicomCanvas, bool>(nameof(IsInverted), false);

    public static readonly StyledProperty<double> ZoomLevelProperty =
        AvaloniaProperty.Register<DicomCanvas, double>(nameof(ZoomLevel), 1.0);

    public static readonly StyledProperty<double> PanXProperty =
        AvaloniaProperty.Register<DicomCanvas, double>(nameof(PanX), 0.0);

    public static readonly StyledProperty<double> PanYProperty =
        AvaloniaProperty.Register<DicomCanvas, double>(nameof(PanY), 0.0);

    public static readonly StyledProperty<double> RotationProperty =
        AvaloniaProperty.Register<DicomCanvas, double>(nameof(Rotation), 0.0);

    public static readonly StyledProperty<bool> IsFlippedHProperty =
        AvaloniaProperty.Register<DicomCanvas, bool>(nameof(IsFlippedH), false);

    public static readonly StyledProperty<bool> IsFlippedVProperty =
        AvaloniaProperty.Register<DicomCanvas, bool>(nameof(IsFlippedV), false);

    public static readonly StyledProperty<MouseTool> ActiveToolProperty =
        AvaloniaProperty.Register<DicomCanvas, MouseTool>(nameof(ActiveTool), MouseTool.Pan);

    // ── Events back to ViewModel ────────────────────────────────────────────────────────────────
    public event EventHandler<double>? ZoomLevelChanged;
    public event EventHandler<(double X, double Y)>? PanChanged;
    public event EventHandler<(double Center, double Width)>? WindowLevelChanged;
    public event EventHandler<int>? FrameScrolled; // +1 or -1

    // ── Public Properties ───────────────────────────────────────────────────────────────────
    public double WindowWidth   { get => GetValue(WindowWidthProperty);   set => SetValue(WindowWidthProperty, value); }
    public double WindowCenter  { get => GetValue(WindowCenterProperty);  set => SetValue(WindowCenterProperty, value); }
    public bool   IsInverted    { get => GetValue(IsInvertedProperty);    set => SetValue(IsInvertedProperty, value); }
    public double ZoomLevel     { get => GetValue(ZoomLevelProperty);     set => SetValue(ZoomLevelProperty, value); }
    public double PanX          { get => GetValue(PanXProperty);          set => SetValue(PanXProperty, value); }
    public double PanY          { get => GetValue(PanYProperty);          set => SetValue(PanYProperty, value); }
    public double Rotation      { get => GetValue(RotationProperty);      set => SetValue(RotationProperty, value); }
    public bool   IsFlippedH    { get => GetValue(IsFlippedHProperty);    set => SetValue(IsFlippedHProperty, value); }
    public bool   IsFlippedV    { get => GetValue(IsFlippedVProperty);    set => SetValue(IsFlippedVProperty, value); }
    public MouseTool ActiveTool { get => GetValue(ActiveToolProperty);    set => SetValue(ActiveToolProperty, value); }

    // ── Private state ───────────────────────────────────────────────────────────────────────────
    private ushort[]? _pixels;
    private int _imgWidth;
    private int _imgHeight;
    private WriteableBitmap? _bitmap;

    // Interaction
    private bool _isDragging;
    private Point _lastPointerPos;
    private readonly List<(Point Start, Point End, double LengthPx)> _measurements = new();
    private Point? _measureStart;
    private string? _annotationText;
    private readonly List<(Point Position, string Text)> _annotations = new();

    // ── Static ctor: which property changes trigger re-render ─────────────────────────────────────
    static DicomCanvas()
    {
        AffectsRender<DicomCanvas>(
            WindowWidthProperty, WindowCenterProperty, IsInvertedProperty,
            ZoomLevelProperty, PanXProperty, PanYProperty,
            RotationProperty, IsFlippedHProperty, IsFlippedVProperty);
    }

    public DicomCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    // ── Public API ──────────────────────────────────────────────────────────────────────────────
    public void SetPixels(ushort[] pixels, int width, int height)
    {
        _pixels = pixels;
        _imgWidth = width;
        _imgHeight = height;
        RebuildBitmap();
        InvalidateVisual();
    }

    public void ClearMeasurements()
    {
        _measurements.Clear();
        _annotations.Clear();
        InvalidateVisual();
    }

    // ── Property change handler ───────────────────────────────────────────────────────────────
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowWidthProperty ||
            change.Property == WindowCenterProperty ||
            change.Property == IsInvertedProperty)
        {
            if (_pixels != null)
            {
                RebuildBitmap();
                InvalidateVisual();
            }
        }

        // Update cursor based on active tool
        if (change.Property == ActiveToolProperty)
            UpdateCursor();
    }

    private void UpdateCursor(bool dragging = false)
    {
        Cursor = ActiveTool switch
        {
            MouseTool.None        => new Cursor(StandardCursorType.Arrow),
            MouseTool.Pan         => dragging ? new Cursor(StandardCursorType.SizeAll) : new Cursor(StandardCursorType.Hand),
            MouseTool.WindowLevel => new Cursor(StandardCursorType.SizeWestEast),
            MouseTool.Measure     => new Cursor(StandardCursorType.Cross),
            MouseTool.Annotate    => new Cursor(StandardCursorType.Ibeam),
            _                     => new Cursor(StandardCursorType.Arrow),
        };
    }

    // ── Bitmap rebuild (window/level + invert) ────────────────────────────────────────────────
    private void RebuildBitmap()
    {
        if (_pixels == null || _imgWidth <= 0 || _imgHeight <= 0) return;

        float winWidth = Math.Max(1f, (float)WindowWidth);
        float winCenter = (float)WindowCenter;
        float min = winCenter - winWidth / 2f;

        byte[] rgba = new byte[_imgWidth * _imgHeight * 4];

        for (int i = 0; i < _pixels.Length; i++)
        {
            float val = _pixels[i];
            byte v;
            if (val <= min) v = 0;
            else if (val >= min + winWidth) v = 255;
            else v = (byte)((val - min) / winWidth * 255f);

            if (IsInverted) v = (byte)(255 - v);

            int idx = i * 4;
            rgba[idx]     = v;   // B
            rgba[idx + 1] = v;   // G
            rgba[idx + 2] = v;   // R
            rgba[idx + 3] = 255; // A
        }

        _bitmap = new WriteableBitmap(
            new PixelSize(_imgWidth, _imgHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using var fb = _bitmap.Lock();
        Marshal.Copy(rgba, 0, fb.Address, rgba.Length);
    }

    // ── Rendering ───────────────────────────────────────────────────────────────────────────────
    public override void Render(DrawingContext ctx)
    {
        var bounds = Bounds;
        ctx.FillRectangle(Brushes.Black, new Rect(bounds.Size));

        if (_bitmap == null) return;

        double cx = bounds.Width  / 2 + PanX;
        double cy = bounds.Height / 2 + PanY;

        double scaleX = bounds.Width  / _imgWidth;
        double scaleY = bounds.Height / _imgHeight;
        double baseScale = Math.Min(scaleX, scaleY);
        double scale = baseScale * ZoomLevel;

        double drawW = _imgWidth  * scale;
        double drawH = _imgHeight * scale;

        // Build transform: rotate + flip around canvas centre + pan
        var transform =
            Matrix.CreateTranslation(-cx, -cy) *
            Matrix.CreateScale(IsFlippedH ? -1 : 1, IsFlippedV ? -1 : 1) *
            Matrix.CreateRotation(Rotation * Math.PI / 180.0) *
            Matrix.CreateTranslation(cx, cy);

        using (ctx.PushTransform(transform))
        {
            ctx.DrawImage(_bitmap, new Rect(cx - drawW / 2, cy - drawH / 2, drawW, drawH));
        }

        // Draw measurements on top (in screen space, not transformed)
        DrawMeasurements(ctx);
        DrawAnnotations(ctx);
    }

    private static readonly IPen MeasurePen  = new Pen(Brushes.Yellow, 1.5);
    private static readonly IPen ActivePen   = new Pen(Brushes.Cyan, 1.5, dashStyle: DashStyle.Dash);
    private static readonly Typeface LabelFont = new(FontFamily.Default);

    private void DrawMeasurements(DrawingContext ctx)
    {
        foreach (var (s, e, len) in _measurements)
        {
            ctx.DrawLine(MeasurePen, s, e);
            // Endpoint dot
            ctx.DrawEllipse(Brushes.Yellow, null, e, 3, 3);
            var label = new FormattedText($"{len:F1}px",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, LabelFont, 10, Brushes.Yellow);
            ctx.DrawText(label, new Point(e.X + 5, e.Y - 14));
        }
    }

    private void DrawAnnotations(DrawingContext ctx)
    {
        foreach (var (pos, text) in _annotations)
        {
            var ft = new FormattedText(text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, LabelFont, 11, Brushes.LimeGreen);
            ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
                new Rect(pos.X - 2, pos.Y - 2, ft.Width + 6, ft.Height + 4));
            ctx.DrawText(ft, pos);
        }
    }

    // ── Input Handling ──────────────────────────────────────────────────────────────────────────
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _lastPointerPos = e.GetPosition(this);
        _isDragging = true;

        if (ActiveTool == MouseTool.None) return; // no tool — ignore all mouse interaction

        if (ActiveTool == MouseTool.Pan)
            UpdateCursor(dragging: true);

        if (ActiveTool == MouseTool.Measure)
            _measureStart = _lastPointerPos;

        if (ActiveTool == MouseTool.Annotate)
        {
            _annotations.Add((_lastPointerPos, "▶ Annotation"));
            InvalidateVisual();
        }

        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragging && ActiveTool == MouseTool.Measure && _measureStart.HasValue)
        {
            var end = e.GetPosition(this);
            var len = Math.Sqrt(Math.Pow(end.X - _measureStart.Value.X, 2) +
                                Math.Pow(end.Y - _measureStart.Value.Y, 2));
            _measurements.Add((_measureStart.Value, end, len));
            _measureStart = null;
            InvalidateVisual();
        }
        _isDragging = false;
        if (ActiveTool == MouseTool.Pan)
            UpdateCursor(dragging: false);
        e.Pointer.Capture(null);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDragging) return;

        var pos = e.GetPosition(this);
        double dx = pos.X - _lastPointerPos.X;
        double dy = pos.Y - _lastPointerPos.Y;

        switch (ActiveTool)
        {
            // Pan tool: drag moves image; scroll (handled in OnPointerWheelChanged) zooms
            case MouseTool.Pan:
                PanX += dx;
                PanY += dy;
                PanChanged?.Invoke(this, (PanX, PanY));
                break;

            case MouseTool.WindowLevel:
                // Horizontal = center, vertical = width (standard DICOM convention)
                WindowCenter += dx * 2.0;
                WindowWidth   = Math.Max(1, WindowWidth + dy * 4.0);
                WindowLevelChanged?.Invoke(this, (WindowCenter, WindowWidth));
                break;
        }

        _lastPointerPos = pos;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (ActiveTool == MouseTool.Pan)
        {
            // Pan tool: scroll zooms, anchored to the pointer position
            double delta = e.Delta.Y > 0 ? 1.12 : 0.89;
            var pos = e.GetPosition(this);
            PanX = pos.X - (pos.X - (Bounds.Width  / 2 + PanX)) * delta - Bounds.Width  / 2;
            PanY = pos.Y - (pos.Y - (Bounds.Height / 2 + PanY)) * delta - Bounds.Height / 2;
            ZoomLevel = Math.Clamp(ZoomLevel * delta, 0.05, 20.0);
            ZoomLevelChanged?.Invoke(this, ZoomLevel);
            PanChanged?.Invoke(this, (PanX, PanY));
        }
        else
        {
            // None + all other tools: scroll navigates frames
            int direction = e.Delta.Y > 0 ? -1 : 1; // scroll up = previous frame
            FrameScrolled?.Invoke(this, direction);
        }

        InvalidateVisual();
    }
}
