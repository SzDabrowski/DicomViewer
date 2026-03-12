using FFMediaToolkit;
using FFMediaToolkit.Decoding;
using FFMediaToolkit.Graphics;
using System;
using System.IO;
using System.Linq;

namespace DicomViewer.Services;

public class VideoService
{
    private static readonly string[] SupportedExtensions = { ".avi", ".mp4", ".mkv", ".mov", ".wmv" };
    private static bool _ffmpegInitialized;
    private static string? _ffmpegError;

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
            @"C:\ffmpeg",
            @"C:\Program Files\ffmpeg\bin",
            @"C:\Program Files\ffmpeg",
        };

        foreach (var dir in candidates)
        {
            if (TrySetFFmpegPath(dir))
            {
                _ffmpegInitialized = true;
                _ffmpegError = null;
                return;
            }
        }

        // Try to find FFmpeg on PATH
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in pathDirs)
        {
            if (TrySetFFmpegPath(dir))
            {
                _ffmpegInitialized = true;
                _ffmpegError = null;
                return;
            }
        }

        _ffmpegError = "FFmpeg libraries not found. Please install FFmpeg and ensure it is on your PATH or placed in one of these directories:\n" +
                       string.Join("\n", candidates);
        _ffmpegInitialized = true;
    }

    private static bool TrySetFFmpegPath(string dir)
    {
        if (!Directory.Exists(dir)) return false;

        // Look for any avcodec DLL (handles different FFmpeg versions like avcodec-59, avcodec-60, avcodec-61, etc.)
        var avcodecDll = Directory.EnumerateFiles(dir, "avcodec-*.dll").FirstOrDefault()
                      ?? Directory.EnumerateFiles(dir, "avcodec.dll").FirstOrDefault();

        if (avcodecDll != null)
        {
            FFmpegLoader.FFmpegPath = dir;
            return true;
        }

        return false;
    }

    public VideoMetadata GetMetadata(string filePath)
    {
        EnsureFFmpeg();

        if (_ffmpegError != null)
            throw new InvalidOperationException(_ffmpegError);

        try
        {
            using var file = MediaFile.Open(filePath);
            var info = file.Video.Info;
            int totalFrames = info.NumberOfFrames ?? (int)(info.Duration.TotalSeconds * info.AvgFrameRate);
            return new VideoMetadata(
                Path.GetFileName(filePath),
                info.FrameSize.Width,
                info.FrameSize.Height,
                Math.Max(1, totalFrames),
                info.AvgFrameRate);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to open video file '{Path.GetFileName(filePath)}': {ex.Message}", ex);
        }
    }

    public ushort[] LoadFrame(string filePath, int frameIndex, out int width, out int height)
    {
        EnsureFFmpeg();

        if (_ffmpegError != null)
            throw new InvalidOperationException(_ffmpegError);

        try
        {
            using var file = MediaFile.Open(filePath);
            var info = file.Video.Info;
            width = info.FrameSize.Width;
            height = info.FrameSize.Height;

            // Seek to target frame by timestamp
            double fps = info.AvgFrameRate > 0 ? info.AvgFrameRate : 30;
            var timestamp = TimeSpan.FromSeconds((double)frameIndex / fps);

            // Clamp to valid range
            if (timestamp > info.Duration)
                timestamp = info.Duration;

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
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to load frame {frameIndex} from '{Path.GetFileName(filePath)}': {ex.Message}", ex);
        }
    }
}

public record VideoMetadata(string FileName, int Width, int Height, int TotalFrames, double Fps);
