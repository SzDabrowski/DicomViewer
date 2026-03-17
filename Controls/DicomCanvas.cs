using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DicomViewer.Models;
using DicomViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace DicomViewer.Controls;

public class DicomCanvas : Control
{
    // ── Styled Properties ──────────────────────────────────────────────────────────────────
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

    public static readonly StyledProperty<bool> ShowAnnotationsProperty =
        AvaloniaProperty.Register<DicomCanvas, bool>(nameof(ShowAnnotations), true);

    public static readonly StyledProperty<int> AnnotationColorIndexProperty =
        AvaloniaProperty.Register<DicomCanvas, int>(nameof(AnnotationColorIndex), 0);

    public static readonly StyledProperty<double> AnnotationStrokeWidthProperty =
        AvaloniaProperty.Register<DicomCanvas, double>(nameof(AnnotationStrokeWidth), 2.0);

    public static readonly StyledProperty<double> AnnotationFontSizeProperty =
        AvaloniaProperty.Register<DicomCanvas, double>(nameof(AnnotationFontSize), 14.0);

    // ── Events back to ViewModel ───────────────────────────────────────────────────────────
    public event EventHandler<double>? ZoomLevelChanged;
    public event EventHandler<(double X, double Y)>? PanChanged;
    public event EventHandler<(double Center, double Width)>? WindowLevelChanged;
    public event EventHandler<int>? FrameScrolled;

    // ── Public Properties ──────────────────────────────────────────────────────────────────
    public double WindowWidth      { get => GetValue(WindowWidthProperty);      set => SetValue(WindowWidthProperty, value); }
    public double WindowCenter     { get => GetValue(WindowCenterProperty);     set => SetValue(WindowCenterProperty, value); }
    public bool   IsInverted       { get => GetValue(IsInvertedProperty);       set => SetValue(IsInvertedProperty, value); }
    public double ZoomLevel        { get => GetValue(ZoomLevelProperty);        set => SetValue(ZoomLevelProperty, value); }
    public double PanX             { get => GetValue(PanXProperty);             set => SetValue(PanXProperty, value); }
    public double PanY             { get => GetValue(PanYProperty);             set => SetValue(PanYProperty, value); }
    public double Rotation         { get => GetValue(RotationProperty);         set => SetValue(RotationProperty, value); }
    public bool   IsFlippedH       { get => GetValue(IsFlippedHProperty);       set => SetValue(IsFlippedHProperty, value); }
    public bool   IsFlippedV       { get => GetValue(IsFlippedVProperty);       set => SetValue(IsFlippedVProperty, value); }
    public MouseTool ActiveTool    { get => GetValue(ActiveToolProperty);       set => SetValue(ActiveToolProperty, value); }
    public bool   ShowAnnotations  { get => GetValue(ShowAnnotationsProperty);  set => SetValue(ShowAnnotationsProperty, value); }
    public int    AnnotationColorIndex  { get => GetValue(AnnotationColorIndexProperty);  set => SetValue(AnnotationColorIndexProperty, value); }
    public double AnnotationStrokeWidth { get => GetValue(AnnotationStrokeWidthProperty); set => SetValue(AnnotationStrokeWidthProperty, value); }
    public double AnnotationFontSize    { get => GetValue(AnnotationFontSizeProperty);    set => SetValue(AnnotationFontSizeProperty, value); }

    // ── Private state ──────────────────────────────────────────────────────────────────────
    private ushort[]? _pixels;
    private int _imgWidth;
    private int _imgHeight;
    private bool _isColor;
    private WriteableBitmap? _bitmap;
    private byte[]? _rgbaBuffer; // Reusable buffer to avoid per-frame allocation

    // Annotation storage
    private readonly List<Annotation> _annotationList = new();
    private readonly Stack<Annotation> _undoStack = new();

    // Interaction state
    private bool _isDragging;
    private Point _lastPointerPos;
    private Point _dragStart;

    // In-progress annotation being drawn
    private Annotation? _activeAnnotation;

    // Text editing state
    private bool _isEditingText;
    private TextAnnotation? _editingTextAnnotation;

    /// <summary>Whether the canvas is currently in text annotation editing mode.</summary>
    public bool IsEditingText => _isEditingText;

    // ── Static ctor ────────────────────────────────────────────────────────────────────────
    static DicomCanvas()
    {
        AffectsRender<DicomCanvas>(
            WindowWidthProperty, WindowCenterProperty, IsInvertedProperty,
            ZoomLevelProperty, PanXProperty, PanYProperty,
            RotationProperty, IsFlippedHProperty, IsFlippedVProperty,
            ShowAnnotationsProperty);
    }

    public DicomCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    // ── Public API ─────────────────────────────────────────────────────────────────────────
    public void SetPixels(ushort[] pixels, int width, int height, bool isColor = false)
    {
        _pixels = pixels;
        _imgWidth = width;
        _imgHeight = height;
        _isColor = isColor;
        // Invalidate buffer when dimensions change
        int requiredSize = width * height * 4;
        if (_rgbaBuffer == null || _rgbaBuffer.Length != requiredSize)
            _rgbaBuffer = new byte[requiredSize];
        RebuildBitmap();
        InvalidateVisual();
    }

    public void ClearAnnotations()
    {
        _annotationList.Clear();
        _undoStack.Clear();
        _activeAnnotation = null;
        InvalidateVisual();
    }

    public void UndoAnnotation()
    {
        if (_annotationList.Count > 0)
        {
            var last = _annotationList[^1];
            _annotationList.RemoveAt(_annotationList.Count - 1);
            _undoStack.Push(last);
            InvalidateVisual();
        }
    }

    public void RedoAnnotation()
    {
        if (_undoStack.Count > 0)
        {
            _annotationList.Add(_undoStack.Pop());
            InvalidateVisual();
        }
    }

    public int AnnotationCount => _annotationList.Count;

    private Color CurrentColor => AnnotationColors.All[Math.Clamp(AnnotationColorIndex, 0, AnnotationColors.All.Length - 1)];

    private bool IsAnnotationTool => ActiveTool is MouseTool.Arrow or MouseTool.TextLabel
        or MouseTool.Freehand or MouseTool.DrawRect or MouseTool.DrawEllipse or MouseTool.DrawLine;

    // ── Property change handler ────────────────────────────────────────────────────────────
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

        if (change.Property == ActiveToolProperty)
        {
            FinishTextEditing();
            UpdateCursor();
        }
    }

    private void UpdateCursor(bool dragging = false)
    {
        Cursor = ActiveTool switch
        {
            MouseTool.None        => new Cursor(StandardCursorType.Arrow),
            MouseTool.Pan         => dragging ? new Cursor(StandardCursorType.SizeAll) : new Cursor(StandardCursorType.Hand),
            MouseTool.WindowLevel => new Cursor(StandardCursorType.SizeWestEast),
            MouseTool.TextLabel   => new Cursor(StandardCursorType.Ibeam),
            MouseTool.Arrow or MouseTool.DrawRect or MouseTool.DrawEllipse
                or MouseTool.DrawLine => new Cursor(StandardCursorType.Cross),
            MouseTool.Freehand    => new Cursor(StandardCursorType.Cross),
            _                     => new Cursor(StandardCursorType.Arrow),
        };
    }

    // ── Bitmap rebuild ─────────────────────────────────────────────────────────────────────
    private void RebuildBitmap()
    {
        if (_pixels == null || _imgWidth <= 0 || _imgHeight <= 0) return;

        int pixelCount = _imgWidth * _imgHeight;
        int requiredSize = pixelCount * 4;
        if (_rgbaBuffer == null || _rgbaBuffer.Length != requiredSize)
            _rgbaBuffer = new byte[requiredSize];

        if (_isColor && _pixels.Length >= pixelCount * 3)
        {
            // Color image: pixels are stored as 3 planes (R, G, B)
            for (int i = 0; i < pixelCount; i++)
            {
                byte r = (byte)(_pixels[i] >> 8);
                byte g = (byte)(_pixels[i + pixelCount] >> 8);
                byte b = (byte)(_pixels[i + pixelCount * 2] >> 8);

                if (IsInverted) { r = (byte)(255 - r); g = (byte)(255 - g); b = (byte)(255 - b); }

                int idx = i * 4;
                _rgbaBuffer[idx]     = b; // B
                _rgbaBuffer[idx + 1] = g; // G
                _rgbaBuffer[idx + 2] = r; // R
                _rgbaBuffer[idx + 3] = 255;
            }
        }
        else
        {
            // Grayscale: apply Window/Level
            float winWidth = Math.Max(1f, (float)WindowWidth);
            float winCenter = (float)WindowCenter;
            float min = winCenter - winWidth / 2f;

            for (int i = 0; i < pixelCount; i++)
            {
                float val = _pixels[i];
                byte v;
                if (val <= min) v = 0;
                else if (val >= min + winWidth) v = 255;
                else v = (byte)((val - min) / winWidth * 255f);

                if (IsInverted) v = (byte)(255 - v);

                int idx = i * 4;
                _rgbaBuffer[idx]     = v; // B
                _rgbaBuffer[idx + 1] = v; // G
                _rgbaBuffer[idx + 2] = v; // R
                _rgbaBuffer[idx + 3] = 255;
            }
        }

        // Reuse bitmap if dimensions match, else create new
        if (_bitmap == null || _bitmap.PixelSize.Width != _imgWidth || _bitmap.PixelSize.Height != _imgHeight)
        {
            _bitmap = new WriteableBitmap(
                new PixelSize(_imgWidth, _imgHeight),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
        }

        using var fb = _bitmap.Lock();
        Marshal.Copy(_rgbaBuffer, 0, fb.Address, requiredSize);
    }

    // ── Rendering ──────────────────────────────────────────────────────────────────────────
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

        var transform =
            Matrix.CreateTranslation(-cx, -cy) *
            Matrix.CreateScale(IsFlippedH ? -1 : 1, IsFlippedV ? -1 : 1) *
            Matrix.CreateRotation(Rotation * Math.PI / 180.0) *
            Matrix.CreateTranslation(cx, cy);

        using (ctx.PushTransform(transform))
        {
            ctx.DrawImage(_bitmap, new Rect(cx - drawW / 2, cy - drawH / 2, drawW, drawH));
        }

        // Draw annotations on top (screen space)
        if (ShowAnnotations)
        {
            foreach (var ann in _annotationList)
                RenderAnnotation(ctx, ann, isPreview: false);

            if (_activeAnnotation != null)
                RenderAnnotation(ctx, _activeAnnotation, isPreview: true);
        }
    }

    private static readonly Typeface LabelFont = new(FontFamily.Default);

    private void RenderAnnotation(DrawingContext ctx, Annotation ann, bool isPreview)
    {
        var brush = new SolidColorBrush(ann.StrokeColor);
        var pen = new Pen(brush, ann.StrokeWidth);
        var dashPen = isPreview ? new Pen(brush, ann.StrokeWidth, new DashStyle(new double[] { 4, 3 }, 0)) : pen;
        var usePen = isPreview ? dashPen : pen;

        switch (ann)
        {
            case ArrowAnnotation arrow:
                DrawArrow(ctx, arrow.Tail, arrow.Head, usePen, brush);
                break;

            case TextAnnotation text:
                DrawTextLabel(ctx, text, brush);
                break;

            case FreehandAnnotation freehand:
                DrawFreehand(ctx, freehand.Points, usePen);
                break;

            case RectangleAnnotation rect:
                ctx.DrawRectangle(null, usePen, rect.GetRect());
                break;

            case EllipseAnnotation ellipse:
                var er = ellipse.GetRect();
                ctx.DrawEllipse(null, usePen, er.Center, er.Width / 2, er.Height / 2);
                break;

            case LineAnnotation line:
                ctx.DrawLine(usePen, line.Start, line.End);
                break;
        }

        // Selection handles
        if (ann.IsSelected && !isPreview)
        {
            var b = ann.GetBounds();
            var selPen = new Pen(Brushes.White, 1, new DashStyle(new double[] { 3, 3 }, 0));
            ctx.DrawRectangle(null, selPen, b);
        }
    }

    private static void DrawArrow(DrawingContext ctx, Point tail, Point head, IPen pen, IBrush brush)
    {
        ctx.DrawLine(pen, tail, head);

        // Arrowhead
        double dx = head.X - tail.X, dy = head.Y - tail.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 3) return;
        double nx = dx / len, ny = dy / len;
        double arrowSize = Math.Min(12, len * 0.3);

        var p1 = new Point(head.X - arrowSize * (nx - ny * 0.4),
                           head.Y - arrowSize * (ny + nx * 0.4));
        var p2 = new Point(head.X - arrowSize * (nx + ny * 0.4),
                           head.Y - arrowSize * (ny - nx * 0.4));

        var geo = new StreamGeometry();
        using (var gc = geo.Open())
        {
            gc.BeginFigure(head, true);
            gc.LineTo(p1);
            gc.LineTo(p2);
            gc.EndFigure(true);
        }
        ctx.DrawGeometry(brush, null, geo);
    }

    private void DrawTextLabel(DrawingContext ctx, TextAnnotation text, IBrush brush)
    {
        var displayText = text.Text;
        if (_isEditingText && _editingTextAnnotation == text)
            displayText += "|"; // cursor

        var ft = new FormattedText(displayText,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, LabelFont, text.FontSize, brush);

        // Background
        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            new Rect(text.Position.X - 3, text.Position.Y - 2, ft.Width + 8, ft.Height + 4));
        ctx.DrawText(ft, text.Position);
    }

    private static void DrawFreehand(DrawingContext ctx, List<Point> points, IPen pen)
    {
        if (points.Count < 2) return;

        var geo = new StreamGeometry();
        using (var gc = geo.Open())
        {
            gc.BeginFigure(points[0], false);
            for (int i = 1; i < points.Count; i++)
                gc.LineTo(points[i]);
            gc.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, geo);
    }

    // ── Input Handling ─────────────────────────────────────────────────────────────────────
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var pos = e.GetPosition(this);
        _lastPointerPos = pos;
        _dragStart = pos;
        _isDragging = true;

        if (ActiveTool == MouseTool.None) return;

        if (ActiveTool == MouseTool.Pan)
            UpdateCursor(dragging: true);

        // Start creating annotation
        if (IsAnnotationTool)
        {
            FinishTextEditing();
            _activeAnnotation = ActiveTool switch
            {
                MouseTool.Arrow => new ArrowAnnotation
                {
                    Tail = pos, Head = pos,
                    StrokeColor = CurrentColor, StrokeWidth = AnnotationStrokeWidth
                },
                MouseTool.TextLabel => null, // handled on release
                MouseTool.Freehand => new FreehandAnnotation
                {
                    Points = new List<Point> { pos },
                    StrokeColor = CurrentColor, StrokeWidth = AnnotationStrokeWidth
                },
                MouseTool.DrawRect => new RectangleAnnotation
                {
                    TopLeft = pos, BottomRight = pos,
                    StrokeColor = CurrentColor, StrokeWidth = AnnotationStrokeWidth
                },
                MouseTool.DrawEllipse => new EllipseAnnotation
                {
                    TopLeft = pos, BottomRight = pos,
                    StrokeColor = CurrentColor, StrokeWidth = AnnotationStrokeWidth
                },
                MouseTool.DrawLine => new LineAnnotation
                {
                    Start = pos, End = pos,
                    StrokeColor = CurrentColor, StrokeWidth = AnnotationStrokeWidth
                },
                _ => null
            };
        }

        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pos = e.GetPosition(this);

        // Finalize annotation
        if (_activeAnnotation != null)
        {
            // Only commit if the user actually dragged (not a micro-click)
            double dragDist = Math.Sqrt(Math.Pow(pos.X - _dragStart.X, 2) + Math.Pow(pos.Y - _dragStart.Y, 2));
            if (dragDist > 3)
            {
                _annotationList.Add(_activeAnnotation);
                _undoStack.Clear(); // new action clears redo
            }
            _activeAnnotation = null;
            InvalidateVisual();
        }
        else if (ActiveTool == MouseTool.TextLabel && _isDragging)
        {
            // Place text label on click
            var textAnn = new TextAnnotation
            {
                Position = pos,
                Text = "",
                FontSize = AnnotationFontSize,
                StrokeColor = CurrentColor, StrokeWidth = AnnotationStrokeWidth
            };
            _annotationList.Add(textAnn);
            _undoStack.Clear();
            _isEditingText = true;
            _editingTextAnnotation = textAnn;
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
            case MouseTool.Pan:
                PanX += dx;
                PanY += dy;
                PanChanged?.Invoke(this, (PanX, PanY));
                break;

            case MouseTool.WindowLevel:
                WindowCenter += dx * 2.0;
                WindowWidth   = Math.Max(1, WindowWidth + dy * 4.0);
                WindowLevelChanged?.Invoke(this, (WindowCenter, WindowWidth));
                break;
        }

        // Update in-progress annotation
        if (_activeAnnotation != null)
        {
            switch (_activeAnnotation)
            {
                case ArrowAnnotation arrow:
                    arrow.Head = pos;
                    break;
                case FreehandAnnotation freehand:
                    freehand.Points.Add(pos);
                    break;
                case RectangleAnnotation rect:
                    rect.BottomRight = pos;
                    break;
                case EllipseAnnotation ellipse:
                    ellipse.BottomRight = pos;
                    break;
                case LineAnnotation line:
                    line.End = pos;
                    break;
            }
        }

        _lastPointerPos = pos;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (ActiveTool == MouseTool.Pan)
        {
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
            int direction = e.Delta.Y > 0 ? -1 : 1;
            FrameScrolled?.Invoke(this, direction);
        }

        InvalidateVisual();
    }

    // ── Text input for TextLabel ───────────────────────────────────────────────────────────
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (_isEditingText && _editingTextAnnotation != null && !string.IsNullOrEmpty(e.Text))
        {
            _editingTextAnnotation.Text += e.Text;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Handle text editing keys
        if (_isEditingText && _editingTextAnnotation != null)
        {
            switch (e.Key)
            {
                case Key.Back:
                    if (_editingTextAnnotation.Text.Length > 0)
                        _editingTextAnnotation.Text = _editingTextAnnotation.Text[..^1];
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                case Key.Enter:
                case Key.Escape:
                    FinishTextEditing();
                    e.Handled = true;
                    return;
            }
            // Don't let other keys propagate while editing text
            if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl)
                e.Handled = true;
            return;
        }

        // Ctrl+Z = Undo, Ctrl+Y / Ctrl+Shift+Z = Redo
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.Z && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                UndoAnnotation();
                e.Handled = true;
            }
            else if (e.Key == Key.Y || (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
            {
                RedoAnnotation();
                e.Handled = true;
            }
        }

        // Delete key removes last annotation
        if (e.Key == Key.Delete && _annotationList.Count > 0)
        {
            var last = _annotationList[^1];
            _annotationList.RemoveAt(_annotationList.Count - 1);
            _undoStack.Push(last);
            InvalidateVisual();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void FinishTextEditing()
    {
        if (!_isEditingText || _editingTextAnnotation == null) return;

        // Remove empty text annotations
        if (string.IsNullOrWhiteSpace(_editingTextAnnotation.Text))
            _annotationList.Remove(_editingTextAnnotation);

        _isEditingText = false;
        _editingTextAnnotation = null;
        InvalidateVisual();
    }
}
