using FellowOakDicom;
using FellowOakDicom.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DicomViewer.Services
{
    // Representation of calibrated US region according to DICOM Part 3, Section C.8.5.5
    public record UltrasoundRegion(
        int X0, int Y0, int X1, int Y1,
        double DeltaX, double DeltaY,
        int Units);

    // Clinical metadata required for diagnostic viewing and measurement
    public record DicomMetadata(
        string PatientName, string PatientId, string StudyDate,
        string Modality, string SeriesDescription, int TotalFrames,
        int Rows, int Columns, double WindowCenter, double WindowWidth,
        bool IsLossy, double? PixelSpacingX, double? PixelSpacingY,
        List<UltrasoundRegion> USRegions);

    public class DicomService
    {
        public DicomMetadata GetMetadata(string filePath)
        {
            var file = DicomFile.Open(filePath);
            var dataset = file.Dataset;

            // Audit Requirement: Verify lossy compression to warn user about potential artifacts
            bool isLossy = dataset.GetSingleValueOrDefault(DicomTag.LossyImageCompression, "00") == "01";
            var regions = new List<UltrasoundRegion>();

            // Extract US Regions for spatial calibration (C.8.5.5)
            if (dataset.Contains(DicomTag.SequenceOfUltrasoundRegions))
            {
                var seq = dataset.GetSequence(DicomTag.SequenceOfUltrasoundRegions);
                foreach (var item in seq.Items)
                {
                    try
                    {
                        regions.Add(new UltrasoundRegion(
                            item.GetSingleValue<int>(DicomTag.RegionLocationMinX0),
                            item.GetSingleValue<int>(DicomTag.RegionLocationMinY0),
                            item.GetSingleValue<int>(DicomTag.RegionLocationMaxX1),
                            item.GetSingleValue<int>(DicomTag.RegionLocationMaxY1),
                            item.GetSingleValue<double>(DicomTag.PhysicalDeltaX),
                            item.GetSingleValue<double>(DicomTag.PhysicalDeltaY),
                            item.GetSingleValue<int>(DicomTag.PhysicalUnitsXDirection)
                        ));
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.Warning("DicomService", "Skipped malformed US region", ex.Message);
                    }
                }
            }

            // Fallback: Extract Standard Pixel Spacing (0028,0030) for CT/MRI
            double? psX = null, psY = null;
            if (dataset.Contains(DicomTag.PixelSpacing))
            {
                var ps = dataset.GetValues<double>(DicomTag.PixelSpacing);
                psY = ps[0]; psX = ps[1];
            }

            return new DicomMetadata(
                dataset.GetSingleValueOrDefault(DicomTag.PatientName, "Unknown"),
                dataset.GetSingleValueOrDefault(DicomTag.PatientID, "000000"),
                dataset.GetSingleValueOrDefault(DicomTag.StudyDate, "N/A"),
                dataset.GetSingleValueOrDefault(DicomTag.Modality, "OT"),
                dataset.GetSingleValueOrDefault(DicomTag.SeriesDescription, ""),
                new DicomImage(dataset).NumberOfFrames,
                dataset.GetSingleValueOrDefault<int>(DicomTag.Rows, 0),
                dataset.GetSingleValueOrDefault<int>(DicomTag.Columns, 0),
                32768, 65535, isLossy, psX, psY, regions);
        }

        public ushort[] LoadDicomPixels(string filePath, int frameIndex, out int width, out int height)
        {
            var file = DicomFile.Open(filePath);
            var dataset = file.Dataset;
            width = dataset.GetSingleValue<int>(DicomTag.Columns);
            height = dataset.GetSingleValue<int>(DicomTag.Rows);

            var image = new DicomImage(dataset);
            var rendered = image.RenderImage(frameIndex);
            var rgba = rendered.AsBytes();
            var pixels = new ushort[width * height];

            // Map 8-bit rendered RGBA to 16-bit internal grayscale range
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = (ushort)(rgba[i * 4 + 2] * 257);

            return pixels;
        }
    }
}