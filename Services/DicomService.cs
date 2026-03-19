using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        // LRU frame cache: avoids re-decoding frames during loop playback
        private const long MaxCacheBytes = 256 * 1024 * 1024; // 256 MB budget
        private readonly Dictionary<(string, int), (ushort[] Pixels, int Width, int Height, bool IsColor)> _frameCache = new();
        private readonly LinkedList<(string Path, int Frame, long Size)> _cacheOrder = new();
        private long _currentCacheBytes;
        private readonly object _cacheLock = new();

        private void CacheFrame(string filePath, int frameIndex, ushort[] pixels, int width, int height, bool isColor)
        {
            long entrySize = pixels.Length * 2L;
            lock (_cacheLock)
            {
                var key = (filePath, frameIndex);
                if (_frameCache.ContainsKey(key)) return;

                // Evict oldest entries until we have room
                while (_currentCacheBytes + entrySize > MaxCacheBytes && _cacheOrder.Count > 0)
                {
                    var oldest = _cacheOrder.First!.Value;
                    _cacheOrder.RemoveFirst();
                    _frameCache.Remove((oldest.Path, oldest.Frame));
                    _currentCacheBytes -= oldest.Size;
                }

                _frameCache[key] = (pixels, width, height, isColor);
                _cacheOrder.AddLast((filePath, frameIndex, entrySize));
                _currentCacheBytes += entrySize;
            }
        }

        private bool TryGetCachedFrame(string filePath, int frameIndex, out ushort[] pixels, out int width, out int height, out bool isColor)
        {
            lock (_cacheLock)
            {
                if (_frameCache.TryGetValue((filePath, frameIndex), out var cached))
                {
                    pixels = cached.Pixels;
                    width = cached.Width;
                    height = cached.Height;
                    isColor = cached.IsColor;
                    return true;
                }
            }
            pixels = Array.Empty<ushort>();
            width = height = 0;
            isColor = false;
            return false;
        }

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _frameCache.Clear();
                _cacheOrder.Clear();
                _currentCacheBytes = 0;
            }
        }

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
                catch (Exception ex)
                {
                    LoggingService.Instance.Debug("DicomService", $"Failed to parse ImageOrientationPatient: {ex.Message}");
                }
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
            // Check cache first to avoid expensive re-decoding
            if (TryGetCachedFrame(filePath, frameIndex, out var cached, out width, out height, out isColor))
                return cached;

            var file = DicomFile.Open(filePath);
            var dataset = file.Dataset;
            width = dataset.GetSingleValue<int>(DicomTag.Columns);
            height = dataset.GetSingleValue<int>(DicomTag.Rows);

            // PROBLEM 1 FIX: Clamp frame index at the service layer as a safety net
            int totalFrames = new DicomImage(dataset).NumberOfFrames;
            if (frameIndex < 0 || frameIndex >= totalFrames)
            {
                LoggingService.Instance.Warning("DicomService",
                    $"Frame {frameIndex} out of range (0-{totalFrames - 1}), clamping");
                frameIndex = Math.Clamp(frameIndex, 0, Math.Max(0, totalFrames - 1));
            }

            string photoInterp = dataset.GetSingleValueOrDefault(DicomTag.PhotometricInterpretation, "MONOCHROME2");
            isColor = photoInterp is "RGB" or "YBR_FULL" or "YBR_FULL_422" or "PALETTE COLOR";

            ushort[] pixels;
            if (isColor)
                pixels = LoadColorPixels(dataset, frameIndex, width, height);
            else
                pixels = LoadGrayscalePixels(dataset, frameIndex, width, height);

            CacheFrame(filePath, frameIndex, pixels, width, height, isColor);
            return pixels;
        }

        // Overload for backward compatibility
        public ushort[] LoadDicomPixels(string filePath, int frameIndex, out int width, out int height)
        {
            return LoadDicomPixels(filePath, frameIndex, out width, out height, out _);
        }

        /// <summary>
        /// Async version of LoadDicomPixels that runs heavy transcoding/decoding on a background thread.
        /// Supports CancellationToken so fast scrolling can cancel in-flight decodes.
        /// Returns (pixels, width, height, isColor).
        /// </summary>
        public async Task<(ushort[] Pixels, int Width, int Height, bool IsColor)> LoadDicomPixelsAsync(
            string filePath, int frameIndex, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var pixels = LoadDicomPixels(filePath, frameIndex, out int w, out int h, out bool isColor);
                // Don't throw after expensive work is done — let the caller check cancellation
                // and decide whether to use the already-loaded pixels.
                return (pixels, w, h, isColor);
            }, ct);
        }

        private ushort[] LoadGrayscalePixels(FellowOakDicom.DicomDataset dataset, int frameIndex, int width, int height)
        {
            // PROBLEM 2 FIX: Decompress encapsulated (compressed) transfer syntax before raw pixel extraction
            var transferSyntax = dataset.InternalTransferSyntax;
            bool isEncapsulated = transferSyntax != DicomTransferSyntax.ImplicitVRLittleEndian
                && transferSyntax != DicomTransferSyntax.ExplicitVRLittleEndian
                && transferSyntax != DicomTransferSyntax.ExplicitVRBigEndian
                && transferSyntax != DicomTransferSyntax.DeflatedExplicitVRLittleEndian;

            if (isEncapsulated)
            {
                try
                {
                    var transcoder = new DicomTranscoder(transferSyntax, DicomTransferSyntax.ExplicitVRLittleEndian);
                    dataset = transcoder.Transcode(dataset);
                    LoggingService.Instance.Debug("DicomService", $"Transcoded from {transferSyntax} to Explicit VR LE");
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.Warning("DicomService",
                        $"Transcoding failed for {transferSyntax}, using fallback renderer", ex.Message);
                    int pixelCount0 = width * height;
                    return LoadFallbackPixels(dataset, frameIndex, pixelCount0);
                }
            }

            int bitsAllocated = dataset.GetSingleValueOrDefault<int>(DicomTag.BitsAllocated, 16);
            int bitsStored = dataset.GetSingleValueOrDefault<int>(DicomTag.BitsStored, bitsAllocated);
            int highBit = dataset.GetSingleValueOrDefault<int>(DicomTag.HighBit, bitsStored - 1);
            int pixelRep = dataset.GetSingleValueOrDefault<int>(DicomTag.PixelRepresentation, 0); // 0=unsigned, 1=signed
            double rescaleSlope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
            double rescaleIntercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);
            int samplesPerPixel = dataset.GetSingleValueOrDefault<int>(DicomTag.SamplesPerPixel, 1);

            int pixelCount = width * height;
            var pixels = new ushort[pixelCount];

            // Try native pixel data extraction (now guaranteed uncompressed after transcoding)
            byte[] rawBytes;
            try
            {
                var pixelData = FellowOakDicom.Imaging.DicomPixelData.Create(dataset);

                // Clamp frame index to available frames
                int availableFrames = pixelData.NumberOfFrames;
                if (frameIndex >= availableFrames)
                {
                    LoggingService.Instance.Warning("DicomService",
                        $"Frame {frameIndex} requested but only {availableFrames} available, clamping");
                    frameIndex = Math.Max(0, availableFrames - 1);
                }

                var frameData = pixelData.GetFrame(frameIndex);
                rawBytes = frameData.Data;
            }
            catch (Exception ex)
            {
                // If raw extraction fails, fall back to fo-dicom rendering
                LoggingService.Instance.Warning("DicomService", "Raw pixel extraction failed, using fallback", ex.Message);
                return LoadFallbackPixels(dataset, frameIndex, pixelCount);
            }

            // Validate raw data size matches expected dimensions
            int expectedBytes16 = pixelCount * 2 * samplesPerPixel;
            int expectedBytes8 = pixelCount * samplesPerPixel;

            // Determine min/max for mapping to ushort range after rescale
            double minVal = double.MaxValue, maxVal = double.MinValue;
            var modalityValues = new double[pixelCount];

            if (bitsAllocated == 16 && rawBytes.Length >= expectedBytes16)
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
            else if (bitsAllocated == 8 && rawBytes.Length >= expectedBytes8)
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
                // Fallback: raw data size mismatch or unusual bit depth — use fo-dicom rendering
                return LoadFallbackPixels(dataset, frameIndex, pixelCount);
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

        private ushort[] LoadFallbackPixels(FellowOakDicom.DicomDataset dataset, int frameIndex, int pixelCount)
        {
            var pixels = new ushort[pixelCount];
            var image = new DicomImage(dataset);
            var rendered = image.RenderImage(frameIndex);
            var rgba = rendered.AsBytes();
            int count = Math.Min(pixelCount, rgba.Length / 4);
            for (int i = 0; i < count; i++)
                pixels[i] = (ushort)(rgba[i * 4 + 2] * 257);
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
        /// Groups a list of DICOM file paths by SeriesInstanceUID and sorts
        /// slices within each series by InstanceNumber or SliceLocation.
        /// Returns a list of series stacks, each containing ordered file paths.
        /// </summary>
        public List<Models.DicomSeriesStack> GroupFilesIntoStacks(string[] filePaths)
        {
            var fileInfos = new List<(string Path, string SeriesUID, int InstanceNumber, double SliceLocation, string SeriesDesc, string Modality)>();

            foreach (var path in filePaths)
            {
                try
                {
                    var file = DicomFile.Open(path);
                    var ds = file.Dataset;
                    string seriesUid = ds.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, "UNKNOWN");
                    int instanceNum = ds.GetSingleValueOrDefault<int>(DicomTag.InstanceNumber, 0);
                    double sliceLoc = ds.GetSingleValueOrDefault<double>(DicomTag.SliceLocation, 0.0);
                    string seriesDesc = ds.GetSingleValueOrDefault(DicomTag.SeriesDescription, "");
                    string modality = ds.GetSingleValueOrDefault(DicomTag.Modality, "OT");

                    fileInfos.Add((path, seriesUid, instanceNum, sliceLoc, seriesDesc, modality));
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.Warning("DicomService",
                        $"Skipping non-DICOM file during stacking: {System.IO.Path.GetFileName(path)}", ex.Message);
                }
            }

            // Group by SeriesInstanceUID
            var stacks = fileInfos
                .GroupBy(f => f.SeriesUID)
                .Select(group =>
                {
                    // Sort by InstanceNumber first; if all are 0, fall back to SliceLocation
                    var sorted = group.All(g => g.InstanceNumber == 0)
                        ? group.OrderBy(g => g.SliceLocation).ToList()
                        : group.OrderBy(g => g.InstanceNumber).ToList();

                    var first = sorted.First(); // Safe: GroupBy guarantees at least one element per group
                    return new Models.DicomSeriesStack
                    {
                        SeriesInstanceUID = group.Key,
                        SeriesDescription = first.SeriesDesc,
                        Modality = first.Modality,
                        FilePaths = sorted.Select(s => s.Path).ToList(),
                        SliceCount = sorted.Count
                    };
                })
                .OrderBy(s => s.SeriesDescription)
                .ToList();

            return stacks;
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

            // Decompress if needed (same logic as LoadGrayscalePixels)
            var transferSyntax = dataset.InternalTransferSyntax;
            bool isEncapsulated = transferSyntax != DicomTransferSyntax.ImplicitVRLittleEndian
                && transferSyntax != DicomTransferSyntax.ExplicitVRLittleEndian
                && transferSyntax != DicomTransferSyntax.ExplicitVRBigEndian
                && transferSyntax != DicomTransferSyntax.DeflatedExplicitVRLittleEndian;

            if (isEncapsulated)
            {
                try
                {
                    var transcoder = new DicomTranscoder(transferSyntax, DicomTransferSyntax.ExplicitVRLittleEndian);
                    dataset = transcoder.Transcode(dataset);
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.Debug("DicomService", $"Transcoding failed in GetModalityRange: {ex.Message}");
                    return (0, 65535);
                }
            }

            int width = dataset.GetSingleValue<int>(DicomTag.Columns);
            int height = dataset.GetSingleValue<int>(DicomTag.Rows);
            int bitsAllocated = dataset.GetSingleValueOrDefault<int>(DicomTag.BitsAllocated, 16);
            int bitsStored = dataset.GetSingleValueOrDefault<int>(DicomTag.BitsStored, bitsAllocated);
            int highBit = dataset.GetSingleValueOrDefault<int>(DicomTag.HighBit, bitsStored - 1);
            int pixelRep = dataset.GetSingleValueOrDefault<int>(DicomTag.PixelRepresentation, 0);
            double rescaleSlope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
            double rescaleIntercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);

            int pixelCount = width * height;
            double minVal = double.MaxValue, maxVal = double.MinValue;

            byte[] rawBytes;
            try
            {
                var pixelData = FellowOakDicom.Imaging.DicomPixelData.Create(dataset);
                var frameData = pixelData.GetFrame(frameIndex);
                rawBytes = frameData.Data;
            }
            catch (Exception ex)
            {
                // Cannot read raw data; return default range
                LoggingService.Instance.Debug("DicomService", $"Raw pixel extraction failed in GetModalityRange: {ex.Message}");
                return (0, 65535);
            }

            if (bitsAllocated == 16 && rawBytes.Length >= pixelCount * 2)
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
            else if (rawBytes.Length >= pixelCount)
            {
                for (int i = 0; i < pixelCount; i++)
                {
                    double val = rawBytes[i] * rescaleSlope + rescaleIntercept;
                    if (val < minVal) minVal = val;
                    if (val > maxVal) maxVal = val;
                }
            }
            else
            {
                // Raw data too small; return safe default
                return (0, 65535);
            }

            return (minVal, maxVal);
        }
    }
}