using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using DicomViewer.Services;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DicomViewer.ViewModels;

public partial class ThumbnailViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isCurrentFrame;
    [ObservableProperty] private double _opacity = 0.6;
    [ObservableProperty] private WriteableBitmap? _thumbnail;

    public int FrameIndex { get; }
    public string FilePath { get; }
    /// <summary>Display index used for stacked series (slice number in the stack).</summary>
    public int FrameDisplayIndex { get; set; } = -1;
    public string Label => $"#{(FrameDisplayIndex >= 0 ? FrameDisplayIndex + 1 : FrameIndex + 1)}";

    public ThumbnailViewModel(int frameIndex, string filePath, bool isCurrent = false)
    {
        FrameIndex = frameIndex;
        FilePath = filePath;
        IsCurrentFrame = isCurrent;
        Opacity = isCurrent ? 1.0 : 0.6;

        // Load thumbnail asynchronously so UI doesn't block
        _ = LoadThumbnailAsync();
    }

    partial void OnIsCurrentFrameChanged(bool value)
    {
        Opacity = value ? 1.0 : 0.6;
    }

    private async Task LoadThumbnailAsync()
    {
        try
        {
            var bmp = await Task.Run(() => RenderThumbnail());
            // Marshal back to UI thread
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Thumbnail = bmp;
            });
        }
        catch (Exception ex)
        {
            DicomViewer.Services.LoggingService.Instance.Warning("Thumbnail", $"Failed to load thumbnail for frame {FrameIndex}", ex.Message);
        }
    }

    private WriteableBitmap? RenderThumbnail()
    {
        const int ThumbSize = 80;

        var svc = new DicomService();
        var pixels = svc.LoadDicomPixels(FilePath, FrameIndex, out int w, out int h);
        if (pixels == null || w <= 0 || h <= 0) return null;

        // Find min/max for auto-windowing on the thumbnail
        ushort min = ushort.MaxValue, max = 0;
        foreach (var p in pixels)
        {
            if (p < min) min = p;
            if (p > max) max = p;
        }
        float range = max - min;
        if (range < 1) range = 1;

        // Scale down to ThumbSize x ThumbSize
        var rgba = new byte[ThumbSize * ThumbSize * 4];
        for (int ty = 0; ty < ThumbSize; ty++)
        {
            for (int tx = 0; tx < ThumbSize; tx++)
            {
                int srcX = tx * w / ThumbSize;
                int srcY = ty * h / ThumbSize;
                int srcIdx = srcY * w + srcX;
                byte v = (byte)((pixels[srcIdx] - min) / range * 255f);
                int dstIdx = (ty * ThumbSize + tx) * 4;
                rgba[dstIdx] = v;     // B
                rgba[dstIdx + 1] = v; // G
                rgba[dstIdx + 2] = v; // R
                rgba[dstIdx + 3] = 255; // A
            }
        }

        var bmp = new WriteableBitmap(
            new PixelSize(ThumbSize, ThumbSize),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using var fb = bmp.Lock();
        Marshal.Copy(rgba, 0, fb.Address, rgba.Length);
        return bmp;
    }
}