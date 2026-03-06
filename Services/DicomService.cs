using FellowOakDicom;
using FellowOakDicom.Imaging;

namespace DicomViewer.Services;

public class DicomService
{
    public ushort[] LoadDicomPixels(string filePath, int frameIndex, out int width, out int height)
    {
        var file = DicomFile.Open(filePath);
        var dataset = file.Dataset;
        width = dataset.GetSingleValue<int>(DicomTag.Columns);
        height = dataset.GetSingleValue<int>(DicomTag.Rows);
        var image = new DicomImage(dataset);
        var rendered = image.RenderImage(frameIndex);
        var rgba = rendered.AsBytes();
        ushort[] pixels = new ushort[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = (ushort)(rgba[i * 4 + 2] * 257);
        return pixels;
    }

    public int GetTotalFrames(string path) { try { return new DicomImage(DicomFile.Open(path).Dataset).NumberOfFrames; } catch { return 1; } }
    public string GetPatientName(string p) => DicomFile.Open(p).Dataset.GetSingleValueOrDefault(DicomTag.PatientName, "Unknown");
    public string GetPatientId(string p) => DicomFile.Open(p).Dataset.GetSingleValueOrDefault(DicomTag.PatientID, "000000");
    public string GetStudyDate(string p) => DicomFile.Open(p).Dataset.GetSingleValueOrDefault(DicomTag.StudyDate, "N/A");
    public string GetModality(string p) => DicomFile.Open(p).Dataset.GetSingleValueOrDefault(DicomTag.Modality, "OT");
    public string GetSeriesDescription(string p) => DicomFile.Open(p).Dataset.GetSingleValueOrDefault(DicomTag.SeriesDescription, "");
}