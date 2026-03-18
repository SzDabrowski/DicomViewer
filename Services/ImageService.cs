using DicomViewer.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;

namespace DicomViewer.Services;

public class ImageService
{
    public static bool IsSupported(string filePath) => FileTypeDetector.IsImage(filePath);

    public ImageMetadata GetMetadata(string filePath)
    {
        using var image = Image.Load<Rgba32>(filePath);
        return new ImageMetadata(
            Path.GetFileName(filePath),
            image.Width,
            image.Height,
            1);
    }

    public ushort[] LoadPixels(string filePath, out int width, out int height)
    {
        using var image = Image.Load<Rgba32>(filePath);
        int w = image.Width, h = image.Height;
        width = w;
        height = h;

        var pixels = new ushort[w * h];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    var px = row[x];
                    byte gray = PixelConversion.RgbToGrayscale(px.R, px.G, px.B);
                    pixels[y * w + x] = PixelConversion.GrayToUshort(gray);
                }
            }
        });
        return pixels;
    }
}

public record ImageMetadata(string FileName, int Width, int Height, int TotalFrames);
