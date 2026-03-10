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
}