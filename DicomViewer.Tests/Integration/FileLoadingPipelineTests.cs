using DicomViewer.Helpers;
using DicomViewer.Services;
using DicomViewer.ViewModels;
using System;
using System.IO;
using Xunit;

namespace DicomViewer.Tests.Integration;

public class FileLoadingPipelineTests
{
    [Fact]
    public void Create_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        var fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dcm");

        Assert.Throws<FileNotFoundException>(() => DicomFileViewModel.Create(fakePath));
    }

    [Fact]
    public void Create_WithEmptyTempFile_ThrowsInvalidOperationException()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dcm");
        try
        {
            File.WriteAllBytes(tempPath, Array.Empty<byte>());

            Assert.Throws<InvalidOperationException>(() => DicomFileViewModel.Create(tempPath));
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void FileTypeDetector_AllSupported_ContainsDicomExtensions()
    {
        var allSupported = FileTypeDetector.AllSupported;

        Assert.Contains(".dcm", allSupported);
        Assert.Contains(".dicom", allSupported);
    }

    [Fact]
    public void FileTypeDetector_AllSupported_ContainsImageExtensions()
    {
        var allSupported = FileTypeDetector.AllSupported;

        Assert.Contains(".jpg", allSupported);
        Assert.Contains(".jpeg", allSupported);
        Assert.Contains(".png", allSupported);
        Assert.Contains(".bmp", allSupported);
    }

    [Fact]
    public void FileTypeDetector_AllSupported_ContainsVideoExtensions()
    {
        var allSupported = FileTypeDetector.AllSupported;

        Assert.Contains(".avi", allSupported);
        Assert.Contains(".mp4", allSupported);
        Assert.Contains(".mkv", allSupported);
    }

    [Fact]
    public void FileTypeDetector_IsSupported_ReturnsTrueForKnownExtensions()
    {
        Assert.True(FileTypeDetector.IsSupported("test.dcm"));
        Assert.True(FileTypeDetector.IsSupported("test.jpg"));
        Assert.True(FileTypeDetector.IsSupported("test.mp4"));
    }

    [Fact]
    public void FileTypeDetector_IsSupported_ReturnsTrueForNoExtension()
    {
        // DICOM files often have no extension
        Assert.True(FileTypeDetector.IsSupported("DICOMFILE"));
    }

    [Theory]
    [InlineData("test.dcm")]
    [InlineData("test.dicom")]
    [InlineData("path/to/file.dcm")]
    public void ImageService_IsSupported_ReturnsFalseForDicomFiles(string filePath)
    {
        Assert.False(ImageService.IsSupported(filePath));
    }

    [Theory]
    [InlineData("test.jpg")]
    [InlineData("test.jpeg")]
    [InlineData("test.png")]
    [InlineData("test.bmp")]
    [InlineData("test.tiff")]
    [InlineData("test.gif")]
    [InlineData("test.webp")]
    public void ImageService_IsSupported_ReturnsTrueForImageFiles(string filePath)
    {
        Assert.True(ImageService.IsSupported(filePath));
    }

    [Theory]
    [InlineData("test.jpg")]
    [InlineData("test.png")]
    [InlineData("test.bmp")]
    public void VideoService_IsSupported_ReturnsFalseForImageFiles(string filePath)
    {
        Assert.False(VideoService.IsSupported(filePath));
    }

    [Theory]
    [InlineData("test.dcm")]
    [InlineData("test.dicom")]
    public void VideoService_IsSupported_ReturnsFalseForDicomFiles(string filePath)
    {
        Assert.False(VideoService.IsSupported(filePath));
    }

    [Theory]
    [InlineData("test.avi")]
    [InlineData("test.mp4")]
    [InlineData("test.mkv")]
    [InlineData("test.mov")]
    [InlineData("test.wmv")]
    public void VideoService_IsSupported_ReturnsTrueForVideoFiles(string filePath)
    {
        Assert.True(VideoService.IsSupported(filePath));
    }

    [Fact]
    public void FileTypeDetector_IsDicom_ReturnsTrueForDicomExtensions()
    {
        Assert.True(FileTypeDetector.IsDicom("test.dcm"));
        Assert.True(FileTypeDetector.IsDicom("test.dicom"));
    }

    [Fact]
    public void FileTypeDetector_IsDicom_ReturnsTrueForNoExtension()
    {
        Assert.True(FileTypeDetector.IsDicom("DICOMFILE"));
    }

    [Fact]
    public void FileTypeDetector_IsDicom_ReturnsFalseForOtherExtensions()
    {
        Assert.False(FileTypeDetector.IsDicom("test.jpg"));
        Assert.False(FileTypeDetector.IsDicom("test.mp4"));
    }

    [Fact]
    public void FileTypeDetector_IsImage_ReturnsFalseForDicomFiles()
    {
        Assert.False(FileTypeDetector.IsImage("test.dcm"));
    }

    [Fact]
    public void FileTypeDetector_IsVideo_ReturnsFalseForImageFiles()
    {
        Assert.False(FileTypeDetector.IsVideo("test.jpg"));
        Assert.False(FileTypeDetector.IsVideo("test.png"));
    }
}
