using FFMediaToolkit;
using FFMediaToolkit.Decoding;
using FFMediaToolkit.Graphics;
using System;
using System.IO;

namespace DicomViewer.Services;

public class VideoService
{
    private static readonly string[] SupportedExtensions = { ".avi", ".mp4", ".mkv", ".mov", ".wmv" };
    private static bool _ffmpegInitialized;

    public static bool IsSupported(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return Array.Exists(SupportedExtensions, e => e == ext);
    }

    private static void EnsureFFmpeg()
    {
        if (_ffmpegInitialized) return;

        // Try common FFmpeg locations on Windows
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "ffmpeg"),
            Path.Combine(AppContext.BaseDirectory),
            @"C:\ffmpeg\bin",
            @"C:\Program Files\ffmpeg\bin",
        };

        foreach (var dir in candidates)
        {
            if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, "avcodec-61.dll")))
            {
                FFmpegLoader.FFmpegPath = dir;
                _ffmpegInitialized = true;
                return;
            }
        }

        // Let FFMediaToolkit search PATH
        _ffmpegInitialized = true;
    }

    public VideoMetadata GetMetadata(string filePath)
    {
        EnsureFFmpeg();
        using var file = MediaFile.Open(filePath);
        var info = file.Video.Info;
        return new VideoMetadata(
            Path.GetFileName(filePath),
            info.FrameSize.Width,
            info.FrameSize.Height,
            (int)(info.Duration.TotalSeconds * info.AvgFrameRate),
            info.AvgFrameRate);
    }

    public ushort[] LoadFrame(string filePath, int frameIndex, out int width, out int height)
    {
        EnsureFFmpeg();
        using var file = MediaFile.Open(filePath);
        var info = file.Video.Info;
        width = info.FrameSize.Width;
        height = info.FrameSize.Height;

        // Seek to target frame by timestamp
        double fps = info.AvgFrameRate > 0 ? info.AvgFrameRate : 30;
        var timestamp = TimeSpan.FromSeconds(frameIndex / fps);
        var frame = file.Video.GetFrame(timestamp);

        int w = width, h = height;
        var pixels = new ushort[w * h];
        var data = frame.Data;

        for (int y = 0; y < h; y++)
        {
            var row = data.Slice(y * frame.Stride, w * 3);
            for (int x = 0; x < w; x++)
            {
                int idx = x * 3;
                byte r = row[idx], g = row[idx + 1], b = row[idx + 2];
                byte gray = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
                pixels[y * w + x] = (ushort)(gray * 257);
            }
        }
        return pixels;
    }
}

public record VideoMetadata(string FileName, int Width, int Height, int TotalFrames, double Fps);
