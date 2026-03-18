using System;
using System.IO;

namespace DicomViewer.Helpers;

/// <summary>
/// Single source of truth for supported file extensions and file type detection.
/// </summary>
public static class FileTypeDetector
{
    public static readonly string[] DicomExtensions = { ".dcm", ".dicom" };

    public static readonly string[] ImageExtensions =
        { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif", ".webp" };

    public static readonly string[] VideoExtensions =
        { ".avi", ".mp4", ".mkv", ".mov", ".wmv" };

    /// <summary>All extensions the application can open.</summary>
    public static readonly string[] AllSupported = BuildAllSupported();

    public static bool IsDicom(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return Array.Exists(DicomExtensions, e => e == ext) || ext == "";
    }

    public static bool IsImage(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return Array.Exists(ImageExtensions, e => e == ext);
    }

    public static bool IsVideo(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return Array.Exists(VideoExtensions, e => e == ext);
    }

    public static bool IsSupported(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return Array.Exists(AllSupported, e => e == ext) || ext == "";
    }

    private static string[] BuildAllSupported()
    {
        var all = new string[DicomExtensions.Length + ImageExtensions.Length + VideoExtensions.Length];
        DicomExtensions.CopyTo(all, 0);
        ImageExtensions.CopyTo(all, DicomExtensions.Length);
        VideoExtensions.CopyTo(all, DicomExtensions.Length + ImageExtensions.Length);
        return all;
    }
}
