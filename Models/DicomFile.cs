using System;
using System.Collections.Generic;

namespace DicomViewer.Models;

public class DicomFile
{
    public string FilePath { get; set; } = string.Empty;

    public string FileName => System.IO.Path.GetFileName(FilePath);

    public string PatientName { get; set; } = "Unknown";
    public string PatientId { get; set; } = string.Empty;
    public string StudyDate { get; set; } = string.Empty;
    public string Modality { get; set; } = "OT";
    public string SeriesDescription { get; set; } = string.Empty;
    public string StudyDescription { get; set; } = string.Empty;

    public int TotalFrames { get; set; } = 1;
    public int Rows { get; set; }
    public int Columns { get; set; }

    public double WindowCenter { get; set; } = 32768;
    public double WindowWidth { get; set; } = 65535;

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
}