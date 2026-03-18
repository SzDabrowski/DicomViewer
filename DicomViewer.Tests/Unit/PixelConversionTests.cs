using DicomViewer.Helpers;
using Xunit;

namespace DicomViewer.Tests.Unit;

public class PixelConversionTests
{
    [Fact]
    public void RgbToGrayscale_Black_ReturnsZero()
    {
        byte result = PixelConversion.RgbToGrayscale(0, 0, 0);

        Assert.Equal(0, result);
    }

    [Fact]
    public void RgbToGrayscale_White_Returns255()
    {
        byte result = PixelConversion.RgbToGrayscale(255, 255, 255);

        Assert.Equal(255, result);
    }

    [Theory]
    [InlineData(255, 0, 0, 76)]   // 0.299 * 255 ≈ 76
    [InlineData(0, 255, 0, 149)]   // 0.587 * 255 ≈ 149
    [InlineData(0, 0, 255, 29)]    // 0.114 * 255 ≈ 29
    public void RgbToGrayscale_PrimaryColors_ReturnsExpectedLuma(
        byte r, byte g, byte b, byte expected)
    {
        byte result = PixelConversion.RgbToGrayscale(r, g, b);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void RgbToGrayscale_MidGray_ReturnsApprox128()
    {
        byte result = PixelConversion.RgbToGrayscale(128, 128, 128);

        // 0.299*128 + 0.587*128 + 0.114*128 = 127.872 → truncated to 127
        Assert.Equal(127, result);
    }

    [Fact]
    public void GrayToUshort_Zero_ReturnsZero()
    {
        ushort result = PixelConversion.GrayToUshort(0);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GrayToUshort_Max_Returns65535()
    {
        ushort result = PixelConversion.GrayToUshort(255);

        Assert.Equal(65535, result);
    }

    [Fact]
    public void GrayToUshort_MidValue_ReturnsScaledValue()
    {
        // 128 * 257 = 32896
        ushort result = PixelConversion.GrayToUshort(128);

        Assert.Equal(32896, result);
    }

    [Fact]
    public void GrayToUshort_One_Returns257()
    {
        ushort result = PixelConversion.GrayToUshort(1);

        Assert.Equal(257, result);
    }
}
