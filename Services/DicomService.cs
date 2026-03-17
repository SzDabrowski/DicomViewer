using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
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
        List<UltrasoundRegion> USRegions,
        // Extended metadata for industry-standard overlay
        string PatientSex, string PatientAge, string PatientBirthDate,
        string InstitutionName, string StudyTime, string StudyDescription,
        string AccessionNumber, string SeriesNumber, string InstanceNumber,
        string SliceLocation, string SliceThickness,
        string PhotometricInterpretation, string TransferSyntax,
        double RescaleSlope, double RescaleIntercept,
        string ReferringPhysician, int BitsStored,
        bool IsColor, string ImageOrientationPatient);

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

            // Read DICOM-embedded Window Center/Width (Critical #3)
            double wc = 32768, ww = 65535;
            if (dataset.Contains(DicomTag.WindowCenter) && dataset.Contains(DicomTag.WindowWidth))
            {
                wc = dataset.GetSingleValueOrDefault(DicomTag.WindowCenter, 32768.0);
                ww = dataset.GetSingleValueOrDefault(DicomTag.WindowWidth, 65535.0);
            }

            // Rescale Slope/Intercept for Hounsfield Units (Critical #2)
            double rescaleSlope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
            double rescaleIntercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);

            // Photometric Interpretation (Critical #4)
            string photoInterp = dataset.GetSingleValueOrDefault(DicomTag.PhotometricInterpretation, "MONOCHROME2");
            bool isColor = photoInterp is "RGB" or "YBR_FULL" or "YBR_FULL_422" or "PALETTE COLOR";

            int bitsStored = dataset.GetSingleValueOrDefault<int>(DicomTag.BitsStored, 8);

            // Image Orientation Patient for orientation markers
            string iop = "";
            if (dataset.Contains(DicomTag.ImageOrientationPatient))
            {
                try
                {
                    var vals = dataset.GetValues<double>(DicomTag.ImageOrientationPatient);
                    iop = string.Join("\\", vals.Select(v => v.ToString("F6")));
                }
                catch { }
            }

            // Transfer syntax
            string transferSyntax = file.FileMetaInfo?.TransferSyntax?.UID?.Name ?? "Unknown";

            return new DicomMetadata(
                dataset.GetSingleValueOrDefault(DicomTag.PatientName, "Unknown"),
                dataset.GetSingleValueOrDefault(DicomTag.PatientID, "000000"),
                dataset.GetSingleValueOrDefault(DicomTag.StudyDate, "N/A"),
                dataset.GetSingleValueOrDefault(DicomTag.Modality, "OT"),
                dataset.GetSingleValueOrDefault(DicomTag.SeriesDescription, ""),
                new DicomImage(dataset).NumberOfFrames,
                dataset.GetSingleValueOrDefault<int>(DicomTag.Rows, 0),
                dataset.GetSingleValueOrDefault<int>(DicomTag.Columns, 0),
                wc, ww, isLossy, psX, psY, regions,
                // Extended metadata
                dataset.GetSingleValueOrDefault(DicomTag.PatientSex, ""),
                dataset.GetSingleValueOrDefault(DicomTag.PatientAge, ""),
                dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, ""),
                dataset.GetSingleValueOrDefault(DicomTag.InstitutionName, ""),
                dataset.GetSingleValueOrDefault(DicomTag.StudyTime, ""),
                dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, ""),
                dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, ""),
                dataset.GetSingleValueOrDefault(DicomTag.SeriesNumber, ""),
                dataset.GetSingleValueOrDefault(DicomTag.InstanceNumber, ""),
                dataset.GetSingleValueOrDefault(DicomTag.SliceLocation, ""),
                dataset.GetSingleValueOrDefault(DicomTag.SliceThickness, ""),
                photoInterp, transferSyntax, rescaleSlope, rescaleIntercept,
                dataset.GetSingleValueOrDefault(DicomTag.ReferringPhysicianName, ""),
                bitsStored, isColor, iop);
        }

        /// <summary>
        /// Load native-depth pixel data from DICOM, applying Rescale Slope/Intercept.
        /// Returns pixel values in modality units (e.g., Hounsfield Units for CT).
        /// For color images, returns RGB packed into ushort array with isColor=true.
        /// </summary>
        public ushort[] LoadDicomPixels(string filePath, int frameIndex, out int width, out int height, out bool isColor)
        {
            var file = DicomFile.Open(filePath);
            var dataset = file.Dataset;
            width = dataset.GetSingleValue<int>(DicomTag.Columns);
            height = dataset.GetSingleValue<int>(DicomTag.Rows);

            string photoInterp = dataset.GetSingleValueOrDefault(DicomTag.PhotometricInterpretation, "MONOCHROME2");
            isColor = photoInterp is "RGB" or "YBR_FULL" or "YBR_FULL_422" or "PALETTE COLOR";

            if (isColor)
            {
                // For color images, use fo-dicom's rendering and preserve RGB channels
                return LoadColorPixels(dataset, frameIndex, width, height);
            }

            // Grayscale: read native bit-depth pixel data
            return LoadGrayscalePixels(dataset, frameIndex, width, height);
        }

        // Overload for backward compatibility
        public ushort[] LoadDicomPixels(string filePath, int frameIndex, out int width, out int height)
        {
            return LoadDicomPixels(filePath, frameIndex, out width, out height, out _);
        }

        private ushort[] LoadGrayscalePixels(FellowOakDicom.DicomDataset dataset, int frameIndex, int width, int height)
        {
            int bitsAllocated = dataset.GetSingleValueOrDefault<int>(DicomTag.BitsAllocated, 16);
            int bitsStored = dataset.GetSingleValueOrDefault<int>(DicomTag.BitsStored, bitsAllocated);
            int highBit = dataset.GetSingleValueOrDefault<int>(DicomTag.HighBit, bitsStored - 1);
            int pixelRep = dataset.GetSingleValueOrDefault<int>(DicomTag.PixelRepresentation, 0); // 0=unsigned, 1=signed
            double rescaleSlope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
            double rescaleIntercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);

            var pixelData = FellowOakDicom.Imaging.DicomPixelData.Create(dataset);
            var frameData = pixelData.GetFrame(frameIndex);
            var rawBytes = frameData.Data;

            int pixelCount = width * height;
            var pixels = new ushort[pixelCount];

            // Determine min/max for mapping to ushort range after rescale
            double minVal = double.MaxValue, maxVal = double.MinValue;
            var modalityValues = new double[pixelCount];

            if (bitsAllocated == 16)
            {
                for (int i = 0; i < pixelCount; i++)
                {
                    int rawVal;
                    if (pixelRep == 1) // signed
                        rawVal = BitConverter.ToInt16(rawBytes, i * 2);
                    else
                        rawVal = BitConverter.ToUInt16(rawBytes, i * 2);

                    // Apply bit mask for BitsStored
                    int mask = (1 << bitsStored) - 1;
                    int shift = highBit - bitsStored + 1;
                    rawVal = (rawVal >> shift) & mask;

                    // Sign extension for signed data
                    if (pixelRep == 1 && (rawVal & (1 << (bitsStored - 1))) != 0)
                        rawVal -= (1 << bitsStored);

                    double modalityVal = rawVal * rescaleSlope + rescaleIntercept;
                    modalityValues[i] = modalityVal;
                    if (modalityVal < minVal) minVal = modalityVal;
                    if (modalityVal > maxVal) maxVal = modalityVal;
                }
            }
            else if (bitsAllocated == 8)
            {
                for (int i = 0; i < pixelCount; i++)
                {
                    int rawVal = rawBytes[i];
                    double modalityVal = rawVal * rescaleSlope + rescaleIntercept;
                    modalityValues[i] = modalityVal;
                    if (modalityVal < minVal) minVal = modalityVal;
                    if (modalityVal > maxVal) maxVal = modalityVal;
                }
            }
            else
            {
                // Fallback: use fo-dicom rendering for unusual bit depths (32-bit, etc.)
                var image = new DicomImage(dataset);
                var rendered = image.RenderImage(frameIndex);
                var rgba = rendered.AsBytes();
                for (int i = 0; i < pixelCount; i++)
                    pixels[i] = (ushort)(rgba[i * 4 + 2] * 257);
                return pixels;
            }

            // Map modality values to ushort range (0-65535)
            // We store the actual modality range so W/L presets work in real units
            double range = maxVal - minVal;
            if (range < 1) range = 1;

            for (int i = 0; i < pixelCount; i++)
            {
                double normalized = (modalityValues[i] - minVal) / range;
                pixels[i] = (ushort)(Math.Clamp(normalized, 0, 1) * 65535.0);
            }

            return pixels;
        }

        private ushort[] LoadColorPixels(FellowOakDicom.DicomDataset dataset, int frameIndex, int width, int height)
        {
            // Use fo-dicom's rendering for color images (handles YBR, PALETTE COLOR conversion)
            var image = new DicomImage(dataset);
            var rendered = image.RenderImage(frameIndex);
            var rgba = rendered.AsBytes();

            int pixelCount = width * height;
            // Pack RGB into separate arrays: R in high byte area, store as interleaved RGB ushort triples
            // For color, we return 3x pixel count (R, G, B planes)
            var pixels = new ushort[pixelCount * 3];
            for (int i = 0; i < pixelCount; i++)
            {
                pixels[i]                  = (ushort)(rgba[i * 4 + 2] * 257); // R
                pixels[i + pixelCount]     = (ushort)(rgba[i * 4 + 1] * 257); // G
                pixels[i + pixelCount * 2] = (ushort)(rgba[i * 4 + 0] * 257); // B
            }
            return pixels;
        }

        /// <summary>
        /// Get the modality value range for proper W/L mapping.
        /// Returns (minModalityValue, maxModalityValue) so the canvas can map
        /// W/L presets in real units (e.g., HU for CT).
        /// </summary>
        public (double min, double max) GetModalityRange(string filePath, int frameIndex)
        {
            var file = DicomFile.Open(filePath);
            var dataset = file.Dataset;
            int width = dataset.GetSingleValue<int>(DicomTag.Columns);
            int height = dataset.GetSingleValue<int>(DicomTag.Rows);
            int bitsAllocated = dataset.GetSingleValueOrDefault<int>(DicomTag.BitsAllocated, 16);
            int bitsStored = dataset.GetSingleValueOrDefault<int>(DicomTag.BitsStored, bitsAllocated);
            int highBit = dataset.GetSingleValueOrDefault<int>(DicomTag.HighBit, bitsStored - 1);
            int pixelRep = dataset.GetSingleValueOrDefault<int>(DicomTag.PixelRepresentation, 0);
            double rescaleSlope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
            double rescaleIntercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);

            var pixelData = FellowOakDicom.Imaging.DicomPixelData.Create(dataset);
            var frameData = pixelData.GetFrame(frameIndex);
            var rawBytes = frameData.Data;

            int pixelCount = width * height;
            double minVal = double.MaxValue, maxVal = double.MinValue;

            if (bitsAllocated == 16)
            {
                for (int i = 0; i < pixelCount; i++)
                {
                    int rawVal = pixelRep == 1
                        ? BitConverter.ToInt16(rawBytes, i * 2)
                        : BitConverter.ToUInt16(rawBytes, i * 2);
                    int mask = (1 << bitsStored) - 1;
                    int shift = highBit - bitsStored + 1;
                    rawVal = (rawVal >> shift) & mask;
                    if (pixelRep == 1 && (rawVal & (1 << (bitsStored - 1))) != 0)
                        rawVal -= (1 << bitsStored);
                    double val = rawVal * rescaleSlope + rescaleIntercept;
                    if (val < minVal) minVal = val;
                    if (val > maxVal) maxVal = val;
                }
            }
            else
            {
                for (int i = 0; i < pixelCount; i++)
                {
                    double val = rawBytes[i] * rescaleSlope + rescaleIntercept;
                    if (val < minVal) minVal = val;
                    if (val > maxVal) maxVal = val;
                }
            }

            return (minVal, maxVal);
        }
    }
}