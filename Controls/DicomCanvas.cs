using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DicomViewer.Constants;
using DicomViewer.Models;
using DicomViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DicomViewer.Controls;

/// <summary>
/// Custom control for rendering DICOM images with annotations.
/// Delegates input handling to <see cref="CanvasInputHandler"/>
/// and annotation drawing to <see cref="AnnotationRenderer"/>.
/// </summary>
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
        AvaloniaProperty.Register<DicomCanvas, double>(nameof(AnnotationStrokeWidth), UIConstants.DefaultAnnotationStrokeWidth);

    public static readonly StyledProperty<double> AnnotationFontSizeProperty =
        AvaloniaProperty.Register<DicomCanvas, double>(nameof(AnnotationFontSize), UIConstants.DefaultAnnotationFontSize);

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
    private byte[]? _rgbaBuffer;

    // Annotation storage
    private readonly List<Annotation> _annotationList = new();
    private readonly Stack<Annotation> _undoStack = new();

    // Delegated input handler
    private readonly CanvasInputHandler _inputHandler;

    /// <summary>Whether the canvas is currently in text annotation editing mode.</summary>
    public bool IsEditingText => _inputHandler.IsEditingText;

    // ── Internal API for CanvasInputHandler ─────────────────────────────────────────────────
    internal Color CurrentColorPublic => AnnotationColors.All[Math.Clamp(AnnotationColorIndex, 0, AnnotationColors.All.Length - 1)];

    internal void UpdateCursorPublic(bool dragging = false)
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

    internal void AddAnnotation(Annotation ann)
    {
        _annotationList.Add(ann);
        _undoStack.Clear();
    }

    internal void RemoveAnnotation(Annotation ann) => _annotationList.Remove(ann);

    internal void RaiseZoomLevelChanged() => ZoomLevelChanged?.Invoke(this, ZoomLevel);
    internal void RaisePanChanged() => PanChanged?.Invoke(this, (PanX, PanY));
    internal void RaiseWindowLevelChanged() => WindowLevelChanged?.Invoke(this, (WindowCenter, WindowWidth));
    internal void RaiseFrameScrolled(int direction) => FrameScrolled?.Invoke(this, direction);

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
        _inputHandler = new CanvasInputHandler(this);
    }

    // ── Public API ─────────────────────────────────────────────────────────────────────────
    public void SetPixels(ushort[] pixels, int width, int height, bool isColor = false)
    {
        _pixels = pixels;
        _imgWidth = width;
        _imgHeight = height;
        _isColor = isColor;
        int requiredSize = width * height * 4;
        if (_rgbaBuffer == null || _rgbaBuffer.Length != requiredSize)
            _rgbaBuffer = new byte[requiredSize];
        RebuildBitmap();
        InvalidateVisual();
    }

    /// <summary>
    /// Accepts a pre-built RGBA buffer (computed off UI thread) and uploads it to the bitmap.
    /// This avoids running the pixel conversion loop on the UI thread during playback.
    /// </summary>
    public void SetPrebuiltRgba(byte[] rgbaBuffer, ushort[] pixels, int width, int height, bool isColor = false)
    {
        _pixels = pixels;
        _imgWidth = width;
        _imgHeight = height;
        _isColor = isColor;
        _rgbaBuffer = rgbaBuffer;

        int requiredSize = width * height * 4;
        if (_bitmap == null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
        }

        using var fb = _bitmap.Lock();
        Marshal.Copy(_rgbaBuffer, 0, fb.Address, requiredSize);
        InvalidateVisual();
    }

    /// <summary>
    /// Builds an RGBA buffer from pixel data. Can be called from any thread.
    /// Uses the provided W/L values rather than reading from styled properties.
    /// </summary>
    public static byte[] BuildRgbaBuffer(ushort[] pixels, int width, int height, bool isColor,
        double windowCenter, double windowWidth, bool isInverted)
    {
        int pixelCount = width * height;
        var rgba = new byte[pixelCount * 4];

        if (isColor && pixels.Length >= pixelCount * 3)
        {
            for (int i = 0; i < pixelCount; i++)
            {
                byte r = (byte)(pixels[i] >> 8);
                byte g = (byte)(pixels[i + pixelCount] >> 8);
                byte b = (byte)(pixels[i + pixelCount * 2] >> 8);
                if (isInverted) { r = (byte)(255 - r); g = (byte)(255 - g); b = (byte)(255 - b); }
                int idx = i * 4;
                rgba[idx]     = b;
                rgba[idx + 1] = g;
                rgba[idx + 2] = r;
                rgba[idx + 3] = 255;
            }
        }
        else
        {
            float winW = Math.Max(1f, (float)windowWidth);
            float winC = (float)windowCenter;
            float min = winC - winW / 2f;

            for (int i = 0; i < pixelCount; i++)
            {
                float val = pixels[i];
                byte v;
                if (val <= min) v = 0;
                else if (val >= min + winW) v = 255;
                else v = (byte)((val - min) / winW * 255f);
                if (isInverted) v = (byte)(255 - v);
                int idx = i * 4;
                rgba[idx] = rgba[idx + 1] = rgba[idx + 2] = v;
                rgba[idx + 3] = 255;
            }
        }

        return rgba;
    }

    public void ClearAnnotations()
    {
        _annotationList.Clear();
        _undoStack.Clear();
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
            _inputHandler.FinishTextEditing();
            UpdateCursorPublic();
        }
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
            for (int i = 0; i < pixelCount; i++)
            {
                byte r = (byte)(_pixels[i] >> 8);
                byte g = (byte)(_pixels[i + pixelCount] >> 8);
                byte b = (byte)(_pixels[i + pixelCount * 2] >> 8);

                if (IsInverted) { r = (byte)(255 - r); g = (byte)(255 - g); b = (byte)(255 - b); }

                int idx = i * 4;
                _rgbaBuffer[idx]     = b;
                _rgbaBuffer[idx + 1] = g;
                _rgbaBuffer[idx + 2] = r;
                _rgbaBuffer[idx + 3] = 255;
            }
        }
        else
        {
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
                _rgbaBuffer[idx]     = v;
                _rgbaBuffer[idx + 1] = v;
                _rgbaBuffer[idx + 2] = v;
                _rgbaBuffer[idx + 3] = 255;
            }
        }

        if (_bitmap == null || _bitmap.PixelSize.Width != _imgWidth || _bitmap.PixelSize.Height != _imgHeight)
        {
            // Dispose the old bitmap to free unmanaged memory before allocating a new one
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(_imgWidth, _imgHeight),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
        }

        using var fb = _bitmap.Lock();
        Marshal.Copy(_rgbaBuffer, 0, fb.Address, requiredSize);
    }

    // ── Rendering (delegates annotation drawing to AnnotationRenderer) ─────────────────────
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

        if (ShowAnnotations)
        {
            foreach (var ann in _annotationList)
                AnnotationRenderer.Render(ctx, ann, isPreview: false, _inputHandler.IsEditingText, _inputHandler.EditingTextAnnotation);

            if (_inputHandler.ActiveAnnotation != null)
                AnnotationRenderer.Render(ctx, _inputHandler.ActiveAnnotation, isPreview: true, false, null);
        }
    }

    // ── Input Handling (delegates to CanvasInputHandler) ────────────────────────────────────
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        _inputHandler.HandlePointerPressed(e);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _inputHandler.HandlePointerReleased(e);
        e.Pointer.Capture(null);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _inputHandler.HandlePointerMoved(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _inputHandler.HandlePointerWheelChanged(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        _inputHandler.HandleTextInput(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_inputHandler.HandleKeyDown(e))
        {
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
}
