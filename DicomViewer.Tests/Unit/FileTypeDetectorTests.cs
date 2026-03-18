using DicomViewer.Helpers;
using Xunit;

namespace DicomViewer.Tests.Unit;

public class FileTypeDetectorTests
{
    [Theory]
    [InlineData("scan.dcm")]
    [InlineData("scan.dicom")]
    public void IsDicom_DicomExtensions_ReturnsTrue(string path)
    {
        Assert.True(FileTypeDetector.IsDicom(path));
    }

    [Theory]
    [InlineData("scan.DCM")]
    [InlineData("scan.Dcm")]
    [InlineData("scan.DICOM")]
    public void IsDicom_CaseInsensitive_ReturnsTrue(string path)
    {
        Assert.True(FileTypeDetector.IsDicom(path));
    }

    [Fact]
    public void IsDicom_NoExtension_ReturnsTrue()
    {
        Assert.True(FileTypeDetector.IsDicom("DICOMFILE"));
    }

    [Fact]
    public void IsDicom_ImageExtension_ReturnsFalse()
    {
        Assert.False(FileTypeDetector.IsDicom("photo.jpg"));
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.jpeg")]
    [InlineData("photo.png")]
    [InlineData("photo.bmp")]
    [InlineData("photo.tiff")]
    [InlineData("photo.tif")]
    [InlineData("photo.gif")]
    [InlineData("photo.webp")]
    public void IsImage_ImageExtensions_ReturnsTrue(string path)
    {
        Assert.True(FileTypeDetector.IsImage(path));
    }

    [Theory]
    [InlineData("photo.JPG")]
    [InlineData("photo.PNG")]
    public void IsImage_CaseInsensitive_ReturnsTrue(string path)
    {
        Assert.True(FileTypeDetector.IsImage(path));
    }

    [Fact]
    public void IsImage_DicomExtension_ReturnsFalse()
    {
        Assert.False(FileTypeDetector.IsImage("scan.dcm"));
    }

    [Theory]
    [InlineData("video.mp4")]
    [InlineData("video.avi")]
    [InlineData("video.mkv")]
    [InlineData("video.mov")]
    [InlineData("video.wmv")]
    public void IsVideo_VideoExtensions_ReturnsTrue(string path)
    {
        Assert.True(FileTypeDetector.IsVideo(path));
    }

    [Theory]
    [InlineData("video.MP4")]
    [InlineData("video.AVI")]
    public void IsVideo_CaseInsensitive_ReturnsTrue(string path)
    {
        Assert.True(FileTypeDetector.IsVideo(path));
    }

    [Fact]
    public void IsVideo_ImageExtension_ReturnsFalse()
    {
        Assert.False(FileTypeDetector.IsVideo("photo.png"));
    }

    [Theory]
    [InlineData("scan.dcm")]
    [InlineData("photo.jpg")]
    [InlineData("video.mp4")]
    public void IsSupported_SupportedExtensions_ReturnsTrue(string path)
    {
        Assert.True(FileTypeDetector.IsSupported(path));
    }

    [Fact]
    public void IsSupported_NoExtension_ReturnsTrue()
    {
        Assert.True(FileTypeDetector.IsSupported("DICOMFILE"));
    }

    [Theory]
    [InlineData("document.txt")]
    [InlineData("data.csv")]
    [InlineData("archive.zip")]
    public void IsSupported_UnsupportedExtensions_ReturnsFalse(string path)
    {
        Assert.False(FileTypeDetector.IsSupported(path));
    }
}
