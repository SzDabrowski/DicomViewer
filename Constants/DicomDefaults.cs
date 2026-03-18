namespace DicomViewer.Constants;

/// <summary>
/// Default DICOM values used when metadata is missing or for non-DICOM images.
/// </summary>
public static class DicomDefaults
{
    public const double WindowCenter = 32768;
    public const double WindowWidth = 65535;
    public const string DefaultModality = "OT";
}
