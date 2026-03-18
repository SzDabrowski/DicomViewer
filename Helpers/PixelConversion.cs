namespace DicomViewer.Helpers;

/// <summary>
/// Shared pixel conversion routines used by ImageService and VideoService.
/// </summary>
public static class PixelConversion
{
    // ITU-R BT.601 luma coefficients
    private const double RedWeight = 0.299;
    private const double GreenWeight = 0.587;
    private const double BlueWeight = 0.114;

    /// <summary>
    /// Converts an RGB triplet to a grayscale byte using BT.601 luma weights.
    /// </summary>
    public static byte RgbToGrayscale(byte r, byte g, byte b)
        => (byte)(RedWeight * r + GreenWeight * g + BlueWeight * b);

    /// <summary>
    /// Converts a grayscale byte to a 16-bit value (maps 0-255 to 0-65535).
    /// </summary>
    public static ushort GrayToUshort(byte gray) => (ushort)(gray * 257);
}
