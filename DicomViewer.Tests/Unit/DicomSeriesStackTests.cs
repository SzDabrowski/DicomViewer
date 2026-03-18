using System;
using System.Collections.Generic;
using DicomViewer.Models;
using Xunit;

namespace DicomViewer.Tests.Unit;

public class DicomSeriesStackTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var stack = new DicomSeriesStack();

        Assert.Equal(string.Empty, stack.SeriesInstanceUID);
        Assert.Equal(string.Empty, stack.SeriesDescription);
        Assert.Equal(string.Empty, stack.Modality);
        Assert.Empty(stack.FilePaths);
        Assert.Equal(0, stack.SliceCount);
    }

    [Fact]
    public void GetFilePathForSlice_ValidIndex_ReturnsCorrectPath()
    {
        var stack = new DicomSeriesStack
        {
            FilePaths = new List<string> { "/a.dcm", "/b.dcm", "/c.dcm" }
        };

        Assert.Equal("/a.dcm", stack.GetFilePathForSlice(0));
        Assert.Equal("/b.dcm", stack.GetFilePathForSlice(1));
        Assert.Equal("/c.dcm", stack.GetFilePathForSlice(2));
    }

    [Fact]
    public void GetFilePathForSlice_NegativeIndex_Throws()
    {
        var stack = new DicomSeriesStack
        {
            FilePaths = new List<string> { "/a.dcm", "/b.dcm" }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => stack.GetFilePathForSlice(-1));
    }

    [Fact]
    public void GetFilePathForSlice_IndexBeyondCount_Throws()
    {
        var stack = new DicomSeriesStack
        {
            FilePaths = new List<string> { "/a.dcm", "/b.dcm" }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => stack.GetFilePathForSlice(2));
    }

    [Fact]
    public void GetFilePathForSlice_EmptyList_Throws()
    {
        var stack = new DicomSeriesStack();

        Assert.Throws<ArgumentOutOfRangeException>(() => stack.GetFilePathForSlice(0));
    }

    [Fact]
    public void SliceCount_CanBeSetIndependentlyOfFilePaths()
    {
        var stack = new DicomSeriesStack
        {
            FilePaths = new List<string> { "/a.dcm", "/b.dcm", "/c.dcm" },
            SliceCount = 3
        };

        Assert.Equal(3, stack.SliceCount);
        Assert.Equal(3, stack.FilePaths.Count);
    }

    [Fact]
    public void Properties_CanBePopulated()
    {
        var stack = new DicomSeriesStack
        {
            SeriesInstanceUID = "1.2.3.4.5",
            SeriesDescription = "Axial CT",
            Modality = "CT",
            FilePaths = new List<string> { "/slice1.dcm", "/slice2.dcm" },
            SliceCount = 2
        };

        Assert.Equal("1.2.3.4.5", stack.SeriesInstanceUID);
        Assert.Equal("Axial CT", stack.SeriesDescription);
        Assert.Equal("CT", stack.Modality);
        Assert.Equal(2, stack.FilePaths.Count);
        Assert.Equal(2, stack.SliceCount);
    }

    [Fact]
    public void GetFilePathForSlice_SingleFile_ReturnsIt()
    {
        var stack = new DicomSeriesStack
        {
            FilePaths = new List<string> { "/only.dcm" }
        };

        Assert.Equal("/only.dcm", stack.GetFilePathForSlice(0));
    }
}
