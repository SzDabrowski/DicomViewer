using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DicomViewer.Services;
using System;
using System.Collections.Generic;
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
    [ObservableProperty] private int _selectedWindowModeIndex;
    [ObservableProperty] private bool _isGeneralSelected = true;
    [ObservableProperty] private bool _isControlsSelected;

    public List<string> WindowModeOptions { get; } = new() { "Windowed", "Maximized", "Fullscreen" };

    public SettingsViewModel()
    {
        _appSettings = _settingsService.Load();
        _defaultDirectory = _appSettings.DefaultDirectory;
        _showTooltips = _appSettings.ShowTooltips;
        _selectedWindowModeIndex = (int)_appSettings.StartupWindowMode;
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

    public bool IsWindowedMode => SelectedWindowModeIndex == 0;
    public bool IsMaximizedMode => SelectedWindowModeIndex == 1;
    public bool IsFullscreenMode => SelectedWindowModeIndex == 2;

    [RelayCommand]
    private void SetWindowMode(string mode)
    {
        SelectedWindowModeIndex = mode switch
        {
            "Maximized" => 1,
            "Fullscreen" => 2,
            _ => 0
        };
    }

    partial void OnShowTooltipsChanged(bool value) => SaveSettings();

    partial void OnSelectedWindowModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsWindowedMode));
        OnPropertyChanged(nameof(IsMaximizedMode));
        OnPropertyChanged(nameof(IsFullscreenMode));
        SaveSettings();
    }

    public StartupWindowMode StartupWindowMode => (StartupWindowMode)SelectedWindowModeIndex;

    private void SaveSettings()
    {
        _appSettings.DefaultDirectory = DefaultDirectory;
        _appSettings.ShowTooltips = ShowTooltips;
        _appSettings.StartupWindowMode = StartupWindowMode;
        _settingsService.Save(_appSettings);
    }
}
