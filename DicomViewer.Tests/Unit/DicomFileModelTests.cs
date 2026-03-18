using System.Collections.Generic;
using DicomViewer.Constants;
using DicomViewer.Models;
using Xunit;

namespace DicomViewer.Tests.Unit;

public class DicomFileModelTests
{
    [Fact]
    public void FileName_ExtractsFromFilePath()
    {
        var file = new DicomFile { FilePath = "/path/to/scan.dcm" };

        Assert.Equal("scan.dcm", file.FileName);
    }

    [Fact]
    public void FileName_EmptyPath_ReturnsEmpty()
    {
        var file = new DicomFile();

        Assert.Equal(string.Empty, file.FileName);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var file = new DicomFile();

        Assert.Equal(string.Empty, file.FilePath);
        Assert.Equal("Unknown", file.PatientName);
        Assert.Equal(string.Empty, file.PatientId);
        Assert.Equal(DicomDefaults.DefaultModality, file.Modality);
        Assert.Equal(1, file.TotalFrames);
        Assert.Equal(DicomDefaults.WindowCenter, file.WindowCenter);
        Assert.Equal(DicomDefaults.WindowWidth, file.WindowWidth);
        Assert.False(file.IsLoaded);
        Assert.Equal("Ready", file.Status);
        Assert.Equal(1.0, file.RescaleSlope);
        Assert.Equal(0.0, file.RescaleIntercept);
        Assert.Equal(16, file.BitsStored);
        Assert.False(file.IsColor);
        Assert.False(file.IsLossy);
        Assert.Null(file.StackFilePaths);
        Assert.False(file.IsStacked);
    }

    [Fact]
    public void ModalityToNormalizedCenter_MapsCorrectly()
    {
        var file = new DicomFile { ModalityMin = -1000, ModalityMax = 1000 };

        // center=0 is midpoint: (0 - (-1000)) / 2000 * 65535 = 32767.5
        double result = file.ModalityToNormalizedCenter(0);

        Assert.Equal(32767.5, result, precision: 1);
    }

    [Fact]
    public void ModalityToNormalizedCenter_AtMin_ReturnsZero()
    {
        var file = new DicomFile { ModalityMin = -1000, ModalityMax = 1000 };

        double result = file.ModalityToNormalizedCenter(-1000);

        Assert.Equal(0, result, precision: 5);
    }

    [Fact]
    public void ModalityToNormalizedCenter_AtMax_Returns65535()
    {
        var file = new DicomFile { ModalityMin = -1000, ModalityMax = 1000 };

        double result = file.ModalityToNormalizedCenter(1000);

        Assert.Equal(65535.0, result, precision: 5);
    }

    [Fact]
    public void ModalityToNormalizedWidth_MapsCorrectly()
    {
        var file = new DicomFile { ModalityMin = -1000, ModalityMax = 1000 };

        // width=500 → 500/2000 * 65535 = 16383.75
        double result = file.ModalityToNormalizedWidth(500);

        Assert.Equal(16383.75, result, precision: 1);
    }

    [Fact]
    public void ModalityToNormalizedWidth_FullRange_Returns65535()
    {
        var file = new DicomFile { ModalityMin = 0, ModalityMax = 65535 };

        double result = file.ModalityToNormalizedWidth(65535);

        Assert.Equal(65535.0, result, precision: 5);
    }

    [Fact]
    public void ModalityToNormalized_DegenerateRange_ClampsToMinRange()
    {
        var file = new DicomFile { ModalityMin = 100, ModalityMax = 100 };

        // range < 1 so range = 1; center: (100-100)/1*65535 = 0
        double center = file.ModalityToNormalizedCenter(100);
        Assert.Equal(0, center, precision: 5);
    }

    [Fact]
    public void IsStacked_NullPaths_ReturnsFalse()
    {
        var file = new DicomFile { StackFilePaths = null };

        Assert.False(file.IsStacked);
    }

    [Fact]
    public void IsStacked_SinglePath_ReturnsFalse()
    {
        var file = new DicomFile { StackFilePaths = new List<string> { "a.dcm" } };

        Assert.False(file.IsStacked);
    }

    [Fact]
    public void IsStacked_MultiplePaths_ReturnsTrue()
    {
        var file = new DicomFile
        {
            StackFilePaths = new List<string> { "a.dcm", "b.dcm", "c.dcm" }
        };

        Assert.True(file.IsStacked);
    }

    [Fact]
    public void GetFilePathForFrame_NotStacked_ReturnsFilePath()
    {
        var file = new DicomFile { FilePath = "/path/scan.dcm" };

        Assert.Equal("/path/scan.dcm", file.GetFilePathForFrame(0));
        Assert.Equal("/path/scan.dcm", file.GetFilePathForFrame(5));
    }

    [Fact]
    public void GetFilePathForFrame_Stacked_ReturnsCorrectPath()
    {
        var file = new DicomFile
        {
            FilePath = "/path/a.dcm",
            StackFilePaths = new List<string> { "/path/a.dcm", "/path/b.dcm", "/path/c.dcm" }
        };

        Assert.Equal("/path/a.dcm", file.GetFilePathForFrame(0));
        Assert.Equal("/path/b.dcm", file.GetFilePathForFrame(1));
        Assert.Equal("/path/c.dcm", file.GetFilePathForFrame(2));
    }

    [Fact]
    public void GetFilePathForFrame_Stacked_ClampsOutOfRange()
    {
        var file = new DicomFile
        {
            StackFilePaths = new List<string> { "/a.dcm", "/b.dcm", "/c.dcm" }
        };

        Assert.Equal("/a.dcm", file.GetFilePathForFrame(-1));
        Assert.Equal("/c.dcm", file.GetFilePathForFrame(99));
    }
}
