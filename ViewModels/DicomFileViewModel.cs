using CommunityToolkit.Mvvm.ComponentModel;
using DicomViewer.Constants;
using DicomViewer.Helpers;
using DicomViewer.Models;
using DicomViewer.Services;
using System;

namespace DicomViewer.ViewModels;

public partial class DicomFileViewModel : ViewModelBase
{
    private readonly DicomFile _model;

    [ObservableProperty] private bool _isSelected;

    // IsActive = alias for IsSelected, used by XAML tab/list highlighting
    public bool IsActive => IsSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsActive));
    }

    public DicomFile Model => _model;
    public string FilePath => _model.FilePath;
    public string FileName => _model.FileName;
    public string PatientName => _model.PatientName;
    public string PatientId => _model.PatientId;
    public string Modality => _model.Modality;
    public string StudyDate => _model.StudyDate;
    public string SeriesDescription => _model.SeriesDescription;
    public int TotalFrames => _model.TotalFrames;

    // Extended metadata for four-corner overlay
    public string PatientSex => _model.PatientSex;
    public string PatientAge => _model.PatientAge;
    public string PatientBirthDate => _model.PatientBirthDate;
    public string InstitutionName => _model.InstitutionName;
    public string StudyTime => _model.StudyTime;
    public string StudyDescription => _model.StudyDescription;
    public string AccessionNumber => _model.AccessionNumber;
    public string SeriesNumber => _model.SeriesNumber;
    public string InstanceNumber => _model.InstanceNumber;
    public string SliceLocation => _model.SliceLocation;
    public string SliceThickness => _model.SliceThickness;
    public bool IsLossy => _model.IsLossy;
    public bool IsColor => _model.IsColor;
    public string ImageOrientationPatient => _model.ImageOrientationPatient;

    public string DisplayName => string.IsNullOrEmpty(PatientName) || PatientName == "Unknown"
        ? FileName
        : PatientName;

    public DicomFileViewModel(DicomFile model)
    {
        _model = model;
    }

    private static readonly string[] SupportedExtensions = FileTypeDetector.AllSupported;

    public static DicomFileViewModel Create(string filePath)
    {
        var log = LoggingService.Instance;
        var loc = LocalizationService.Instance;

        // Guard: file must exist
        if (!System.IO.File.Exists(filePath))
            throw new System.IO.FileNotFoundException($"{loc["Err_FileNotFound"]} {filePath}");

        // Guard: file must not be empty
        var fileInfo = new System.IO.FileInfo(filePath);
        if (fileInfo.Length == 0)
            throw new InvalidOperationException($"{loc["Err_FileEmpty"]} {fileInfo.Name}");

        // Guard: file must not be too large (>2 GB)
        if (fileInfo.Length > 2L * 1024 * 1024 * 1024)
            log.Warning("FileOpen", $"{loc["Err_VeryLargeFile"]} ({fileInfo.Length / (1024 * 1024)} MB): {fileInfo.Name}");

        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

        // Guard: check supported extension
        if (!Array.Exists(SupportedExtensions, e => e == ext) && ext != "")
            log.Warning("FileOpen", $"{loc["Err_UnrecognizedExtension"]} '{ext}', {loc["Err_AttemptingDicomParse"]}");

        if (ImageService.IsSupported(filePath))
        {
            var imgSvc = new ImageService();
            var imgMeta = imgSvc.GetMetadata(filePath);
            return new DicomFileViewModel(new DicomFile
            {
                FilePath = filePath,
                Modality = "IMG",
                SeriesDescription = ext.ToUpperInvariant().TrimStart('.'),
                TotalFrames = imgMeta.TotalFrames,
                Rows = imgMeta.Height,
                Columns = imgMeta.Width,
                WindowCenter = DicomDefaults.WindowCenter,
                WindowWidth = DicomDefaults.WindowWidth,
                IsLoaded = true
            });
        }

        if (VideoService.IsSupported(filePath))
        {
            var vidSvc = new VideoService();
            var vidMeta = vidSvc.GetMetadata(filePath);
            return new DicomFileViewModel(new DicomFile
            {
                FilePath = filePath,
                Modality = "VID",
                SeriesDescription = ext.ToUpperInvariant().TrimStart('.'),
                TotalFrames = Math.Max(1, vidMeta.TotalFrames),
                Rows = vidMeta.Height,
                Columns = vidMeta.Width,
                WindowCenter = DicomDefaults.WindowCenter,
                WindowWidth = DicomDefaults.WindowWidth,
                IsLoaded = true
            });
        }

        // Default: attempt DICOM parse
        var svc = new DicomService();
        var meta = svc.GetMetadata(filePath);

        // Get modality value range (e.g., HU range for CT) so we can convert W/L to normalized space
        var (modalityMin, modalityMax) = svc.GetModalityRange(filePath, 0);

        // Store original DICOM W/L and compute normalized values for the canvas
        var model = new DicomFile
        {
            FilePath = filePath,
            PatientName = meta.PatientName,
            PatientId = meta.PatientId,
            StudyDate = meta.StudyDate,
            Modality = meta.Modality,
            SeriesDescription = meta.SeriesDescription,
            TotalFrames = meta.TotalFrames,
            Rows = meta.Rows,
            Columns = meta.Columns,
            DicomWindowCenter = meta.WindowCenter,
            DicomWindowWidth = meta.WindowWidth,
            ModalityMin = modalityMin,
            ModalityMax = modalityMax,
            // Extended metadata
            PatientSex = meta.PatientSex,
            PatientAge = meta.PatientAge,
            PatientBirthDate = meta.PatientBirthDate,
            InstitutionName = meta.InstitutionName,
            StudyTime = meta.StudyTime,
            StudyDescription = meta.StudyDescription,
            AccessionNumber = meta.AccessionNumber,
            SeriesNumber = meta.SeriesNumber,
            InstanceNumber = meta.InstanceNumber,
            SliceLocation = meta.SliceLocation,
            SliceThickness = meta.SliceThickness,
            PhotometricInterpretation = meta.PhotometricInterpretation,
            TransferSyntax = meta.TransferSyntax,
            RescaleSlope = meta.RescaleSlope,
            RescaleIntercept = meta.RescaleIntercept,
            ReferringPhysician = meta.ReferringPhysician,
            BitsStored = meta.BitsStored,
            IsLossy = meta.IsLossy,
            IsColor = meta.IsColor,
            ImageOrientationPatient = meta.ImageOrientationPatient,
            PixelSpacingX = meta.PixelSpacingX,
            PixelSpacingY = meta.PixelSpacingY,
            IsLoaded = true
        };

        // Convert DICOM W/L (in modality units like HU) to normalized 0-65535 pixel space
        // Auto-compute from pixel data range if tags are missing or nonsensical
        double wc = meta.WindowCenter;
        double ww = meta.WindowWidth;
        bool needsAutoCompute = double.IsNaN(wc) || double.IsNaN(ww);

        if (!needsAutoCompute)
        {
            // Check if DICOM W/L values produce normalized results outside valid range
            // This catches cases like WC=32768/WW=65535 on 8-bit data (range 0..255)
            double testWc = model.ModalityToNormalizedCenter(wc);
            double testWw = model.ModalityToNormalizedWidth(ww);
            if (testWc < -65535 || testWc > 2 * 65535 || testWw > 2 * 65535)
                needsAutoCompute = true;
        }

        if (needsAutoCompute)
        {
            wc = (modalityMin + modalityMax) / 2.0;
            ww = modalityMax - modalityMin;
            if (ww < 1) ww = 1;
        }
        model.WindowCenter = model.ModalityToNormalizedCenter(wc);
        model.WindowWidth = model.ModalityToNormalizedWidth(ww);

        log.Debug("FileOpen",
            $"W/L mapping: DICOM WC={meta.WindowCenter:F0} WW={meta.WindowWidth:F0} → " +
            $"Normalized WC={model.WindowCenter:F0} WW={model.WindowWidth:F0} " +
            $"(modality range: {modalityMin:F0}..{modalityMax:F0})");

        return new DicomFileViewModel(model);
    }
}