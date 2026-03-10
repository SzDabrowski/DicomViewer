using CommunityToolkit.Mvvm.ComponentModel;
using DicomViewer.Models;
using DicomViewer.Services;

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

    public string DisplayName => string.IsNullOrEmpty(PatientName) || PatientName == "Unknown"
        ? FileName
        : PatientName;

    public DicomFileViewModel(DicomFile model)
    {
        _model = model;
    }

    public static DicomFileViewModel Create(string filePath)
    {
        var svc = new DicomService();
        var meta = svc.GetMetadata(filePath);
        var model = new DicomFile
        {
            FilePath = filePath,
            PatientName = meta.PatientName,
            PatientId = meta.PatientId,
            StudyDate = meta.StudyDate,
            Modality = meta.Modality,
            SeriesDescription = meta.SeriesDescription,
            TotalFrames = meta.TotalFrames,
            IsLoaded = true
        };
        return new DicomFileViewModel(model);
    }
}