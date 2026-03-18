using DicomViewer.Constants;
using System;
using System.Collections.Generic;

namespace DicomViewer.Models;

/// <summary>
/// Represents a virtual stack of single-frame DICOM files grouped by SeriesInstanceUID.
/// Enables scrolling through a series of slices as if they were a single multi-frame file.
/// </summary>
public class DicomSeriesStack
{
    public string SeriesInstanceUID { get; set; } = string.Empty;
    public string SeriesDescription { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public List<string> FilePaths { get; set; } = new();
    public int SliceCount { get; set; }

    /// <summary>Gets the file path for a given slice index within the stack.</summary>
    public string GetFilePathForSlice(int sliceIndex)
    {
        if (sliceIndex < 0 || sliceIndex >= FilePaths.Count)
            throw new ArgumentOutOfRangeException(nameof(sliceIndex),
                $"Slice {sliceIndex} out of range (0-{FilePaths.Count - 1})");
        return FilePaths[sliceIndex];
    }
}

public class DicomFile
{
    public string FilePath { get; set; } = string.Empty;

    public string FileName => System.IO.Path.GetFileName(FilePath);

    public string PatientName { get; set; } = "Unknown";
    public string PatientId { get; set; } = string.Empty;
    public string StudyDate { get; set; } = string.Empty;
    public string Modality { get; set; } = DicomDefaults.DefaultModality;
    public string SeriesDescription { get; set; } = string.Empty;
    public string StudyDescription { get; set; } = string.Empty;

    public int TotalFrames { get; set; } = 1;
    public int Rows { get; set; }
    public int Columns { get; set; }

    public double WindowCenter { get; set; } = DicomDefaults.WindowCenter;
    public double WindowWidth { get; set; } = DicomDefaults.WindowWidth;

    public bool IsLoaded { get; set; }
    public string Status { get; set; } = "Ready";

    // Extended metadata for four-corner overlay (DICOM industry standard)
    public string PatientSex { get; set; } = string.Empty;
    public string PatientAge { get; set; } = string.Empty;
    public string PatientBirthDate { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
    public string StudyTime { get; set; } = string.Empty;
    public string AccessionNumber { get; set; } = string.Empty;
    public string SeriesNumber { get; set; } = string.Empty;
    public string InstanceNumber { get; set; } = string.Empty;
    public string SliceLocation { get; set; } = string.Empty;
    public string SliceThickness { get; set; } = string.Empty;
    public string PhotometricInterpretation { get; set; } = "MONOCHROME2";
    public string TransferSyntax { get; set; } = string.Empty;
    public double RescaleSlope { get; set; } = 1.0;
    public double RescaleIntercept { get; set; } = 0.0;
    public string ReferringPhysician { get; set; } = string.Empty;
    public int BitsStored { get; set; } = 16;
    public bool IsLossy { get; set; }
    public bool IsColor { get; set; }
    public string ImageOrientationPatient { get; set; } = string.Empty;
    public double? PixelSpacingX { get; set; }
    public double? PixelSpacingY { get; set; }

    // Series stacking: ordered list of file paths for virtual multi-slice navigation
    public List<string>? StackFilePaths { get; set; }

    /// <summary>
    /// True if this file represents a stacked series of single-frame files.
    /// When true, TotalFrames == StackFilePaths.Count and each "frame" is a different file.
    /// </summary>
    public bool IsStacked => StackFilePaths != null && StackFilePaths.Count > 1;

    /// <summary>Gets the file path to use for a given frame/slice index.</summary>
    public string GetFilePathForFrame(int frameIndex)
    {
        if (IsStacked)
        {
            int idx = Math.Clamp(frameIndex, 0, StackFilePaths!.Count - 1);
            return StackFilePaths[idx];
        }
        return FilePath;
    }
}