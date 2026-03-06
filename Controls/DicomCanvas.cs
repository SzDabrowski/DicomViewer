using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Runtime.InteropServices;

namespace DicomViewer.Controls;

public class DicomCanvas : Control
{
    public static readonly StyledProperty<double> WindowWidthProperty =
        AvaloniaProperty.Register<DicomCanvas, double>(nameof(WindowWidth), 65000);
    public static readonly StyledProperty<double> WindowCenterProperty =
        AvaloniaProperty.Register<DicomCanvas, double>(nameof(WindowCenter), 32000);
    public static readonly StyledProperty<bool> IsInvertedProperty =
        AvaloniaProperty.Register<DicomCanvas, bool>(nameof(IsInverted), false);

    public double WindowWidth { get => GetValue(WindowWidthProperty); set => SetValue(WindowWidthProperty, value); }
    public double WindowCenter { get => GetValue(WindowCenterProperty); set => SetValue(WindowCenterProperty, value); }
    public bool IsInverted { get => GetValue(IsInvertedProperty); set => SetValue(IsInvertedProperty, value); }

    private ushort[]? _pixels;
    private int _imgWidth, _imgHeight;
    private WriteableBitmap? _bitmap;

    static DicomCanvas()
    {
        AffectsRender<DicomCanvas>(WindowWidthProperty, WindowCenterProperty, IsInvertedProperty);
    }

    public void SetPixels(ushort[] pixels, int width, int height)
    {
        _pixels = pixels; _imgWidth = width; _imgHeight = height;
        RebuildBitmap(); InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == WindowWidthProperty || change.Property == WindowCenterProperty || change.Property == IsInvertedProperty) && _pixels != null)
        { RebuildBitmap(); InvalidateVisual(); }
    }

    private void RebuildBitmap()
    {
        if (_pixels == null || _imgWidth <= 0 || _imgHeight <= 0) return;
        float winWidth = (float)WindowWidth, winCenter = (float)WindowCenter;
        float min = winCenter - winWidth / 2f, max = winCenter + winWidth / 2f;
        byte[] rgba = new byte[_imgWidth * _imgHeight * 4];
        for (int i = 0; i < _pixels.Length; i++)
        {
            float val = _pixels[i];
            byte v = val <= min ? (byte)0 : val >= max ? (byte)255 : (byte)((val - min) / winWidth * 255f);
            if (IsInverted) v = (byte)(255 - v);
            int idx = i * 4;
            rgba[idx] = v; rgba[idx + 1] = v; rgba[idx + 2] = v; rgba[idx + 3] = 255;
        }
        _bitmap = new WriteableBitmap(new PixelSize(_imgWidth, _imgHeight), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var fb = _bitmap.Lock();
        Marshal.Copy(rgba, 0, fb.Address, rgba.Length);
    }

    public override void Render(DrawingContext ctx)
    {
        var bounds = Bounds;
        ctx.FillRectangle(Brushes.Black, new Rect(bounds.Size));
        if (_bitmap == null) return;
        double scale = Math.Min(bounds.Width / _imgWidth, bounds.Height / _imgHeight);
        double drawW = _imgWidth * scale, drawH = _imgHeight * scale;
        ctx.DrawImage(_bitmap, new Rect((bounds.Width - drawW) / 2, (bounds.Height - drawH) / 2, drawW, drawH));
    }
}