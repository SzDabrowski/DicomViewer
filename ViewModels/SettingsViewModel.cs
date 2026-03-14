using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DicomViewer.Services;
using System;
using System.Threading.Tasks;

namespace DicomViewer.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public Func<Task<string?>>? RequestBrowseDirectory { get; set; }
    public Action? RequestClose { get; set; }

    private readonly SettingsService _settingsService = new();
    private AppSettings _appSettings = new();

    [ObservableProperty] private string _selectedCategory = "General";
    [ObservableProperty] private string _defaultDirectory = string.Empty;
    [ObservableProperty] private bool _showTooltips = true;
    [ObservableProperty] private bool _isGeneralSelected = true;
    [ObservableProperty] private bool _isControlsSelected;

    public SettingsViewModel()
    {
        _appSettings = _settingsService.Load();
        _defaultDirectory = _appSettings.DefaultDirectory;
        _showTooltips = _appSettings.ShowTooltips;
    }

    [RelayCommand]
    private void SelectCategory(string category)
    {
        SelectedCategory = category;
        IsGeneralSelected = category == "General";
        IsControlsSelected = category == "Controls";
    }

    [RelayCommand]
    private async Task BrowseDefaultDirectory()
    {
        if (RequestBrowseDirectory != null)
        {
            var dir = await RequestBrowseDirectory();
            if (!string.IsNullOrEmpty(dir))
            {
                DefaultDirectory = dir;
                SaveSettings();
            }
        }
    }

    [RelayCommand]
    private void ClearDefaultDirectory()
    {
        DefaultDirectory = string.Empty;
        SaveSettings();
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke();

    partial void OnShowTooltipsChanged(bool value) => SaveSettings();

    private void SaveSettings()
    {
        _appSettings.DefaultDirectory = DefaultDirectory;
        _appSettings.ShowTooltips = ShowTooltips;
        _settingsService.Save(_appSettings);
    }
}
