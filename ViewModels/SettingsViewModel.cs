using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DicomViewer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DicomViewer.ViewModels;

public partial class KeyBindingRowViewModel : ObservableObject
{
    private readonly Action _onChanged;

    [ObservableProperty] private string _label;
    [ObservableProperty] private string _displayString;
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private bool _hasConflict;
    [ObservableProperty] private string _conflictMessage = string.Empty;

    public LocalizationService Loc => LocalizationService.Instance;
    public string LabelKey { get; }
    public string PropertyName { get; }
    public KeyBinding Binding { get; }

    public KeyBindingRowViewModel(string labelKey, string propertyName, KeyBinding binding, Action onChanged)
    {
        LabelKey = labelKey;
        _label = LocalizationService.Instance[labelKey];
        PropertyName = propertyName;
        Binding = binding;
        _displayString = binding.DisplayString;
        _onChanged = onChanged;
    }

    public void RefreshLabel()
    {
        Label = LocalizationService.Instance[LabelKey];
    }

    public void StartRecording() => IsRecording = true;

    public void ApplyKey(Avalonia.Input.Key key, Avalonia.Input.KeyModifiers modifiers)
    {
        // Ignore bare modifier keys
        if (key is Avalonia.Input.Key.LeftCtrl or Avalonia.Input.Key.RightCtrl
            or Avalonia.Input.Key.LeftShift or Avalonia.Input.Key.RightShift
            or Avalonia.Input.Key.LeftAlt or Avalonia.Input.Key.RightAlt)
            return;

        Binding.Key = key.ToString();
        Binding.Ctrl = modifiers.HasFlag(Avalonia.Input.KeyModifiers.Control);
        Binding.Shift = modifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift);
        Binding.Alt = modifiers.HasFlag(Avalonia.Input.KeyModifiers.Alt);
        DisplayString = Binding.DisplayString;
        IsRecording = false;
        _onChanged();
    }

    public void CancelRecording() => IsRecording = false;
}

public partial class SettingsViewModel : ViewModelBase
{
    public Func<Task<string?>>? RequestBrowseDirectory { get; set; }
    public Action? RequestClose { get; set; }

    private readonly SettingsService _settingsService = new();
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private AppSettings _appSettings = new();

    [ObservableProperty] private string _selectedCategory = "General";
    [ObservableProperty] private string _defaultDirectory = string.Empty;
    [ObservableProperty] private bool _showTooltips = true;
    [ObservableProperty] private bool _showNotifications = true;
    [ObservableProperty] private int _selectedWindowModeIndex;
    [ObservableProperty] private bool _isGeneralSelected = true;
    [ObservableProperty] private bool _isControlsSelected;
    [ObservableProperty] private int _selectedLanguageIndex;
    [ObservableProperty] private bool _hasUnsavedChanges;
    [ObservableProperty] private bool _showSaveConfirmation;

    private Timer? _confirmationTimer;

    public List<string> WindowModeOptions { get; } = new() { "Windowed", "Maximized", "Fullscreen" };
    public List<string> LanguageOptions { get; } = new() { "English", "Polski" };

    public ObservableCollection<KeyBindingRowViewModel> PlaybackBindings { get; } = new();
    public ObservableCollection<KeyBindingRowViewModel> ViewBindings { get; } = new();
    public ObservableCollection<KeyBindingRowViewModel> ToolBindings { get; } = new();
    public ObservableCollection<KeyBindingRowViewModel> FileBindings { get; } = new();
    public ObservableCollection<KeyBindingRowViewModel> EditBindings { get; } = new();

    public SettingsViewModel()
    {
        _appSettings = _settingsService.Load();
        _defaultDirectory = _appSettings.DefaultDirectory;
        _showTooltips = _appSettings.ShowTooltips;
        _showNotifications = _appSettings.ShowNotifications;
        _selectedWindowModeIndex = (int)_appSettings.StartupWindowMode;
        _selectedLanguageIndex = _appSettings.Language == "pl" ? 1 : 0;
        BuildKeyBindingRows();
    }

    private void BuildKeyBindingRows()
    {
        var kb = _appSettings.KeyBindings;
        Action save = () => { HasUnsavedChanges = true; CheckForConflicts(); };

        PlaybackBindings.Add(new("Controls_PlayPause", "TogglePlay", kb.TogglePlay, save));
        PlaybackBindings.Add(new("Controls_PreviousFrame", "PreviousFrame", kb.PreviousFrame, save));
        PlaybackBindings.Add(new("Controls_NextFrame", "NextFrame", kb.NextFrame, save));
        PlaybackBindings.Add(new("Controls_FirstFrame", "FirstFrame", kb.FirstFrame, save));
        PlaybackBindings.Add(new("Controls_LastFrame", "LastFrame", kb.LastFrame, save));

        ViewBindings.Add(new("Controls_ZoomIn", "ZoomIn", kb.ZoomIn, save));
        ViewBindings.Add(new("Controls_ZoomOut", "ZoomOut", kb.ZoomOut, save));
        ViewBindings.Add(new("Controls_FitToWindow", "FitToWindow", kb.FitToWindow, save));
        ViewBindings.Add(new("Controls_ResetView", "ResetView", kb.ResetView, save));
        ViewBindings.Add(new("Controls_ToggleInvert", "ToggleInvert", kb.ToggleInvert, save));
        ViewBindings.Add(new("Controls_ToggleFullscreen", "ToggleFullscreen", kb.ToggleFullscreen, save));

        ToolBindings.Add(new("Controls_ArrowTool", "ToolArrow", kb.ToolArrow, save));
        ToolBindings.Add(new("Controls_TextTool", "ToolText", kb.ToolText, save));
        ToolBindings.Add(new("Controls_FreehandTool", "ToolFreehand", kb.ToolFreehand, save));
        ToolBindings.Add(new("Controls_CycleColor", "CycleColor", kb.CycleColor, save));
        ToolBindings.Add(new("Controls_DeselectTool", "DeselectTool", kb.DeselectTool, save));

        FileBindings.Add(new("Controls_OpenFile", "OpenFile", kb.OpenFile, save));
        FileBindings.Add(new("Controls_OpenLogs", "OpenLogs", kb.OpenLogs, save));

        EditBindings.Add(new("Controls_Undo", "Undo", kb.Undo, save));
        EditBindings.Add(new("Controls_Redo", "Redo", kb.Redo, save));

        CheckForConflicts();
    }

    [RelayCommand]
    private void ResetKeyBindings()
    {
        _appSettings.KeyBindings = new KeyBindingSettings();
        PlaybackBindings.Clear();
        ViewBindings.Clear();
        ToolBindings.Clear();
        FileBindings.Clear();
        EditBindings.Clear();
        BuildKeyBindingRows();
        HasUnsavedChanges = true;
        CheckForConflicts();
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
                HasUnsavedChanges = true;
            }
        }
    }

    [RelayCommand]
    private void ClearDefaultDirectory()
    {
        DefaultDirectory = string.Empty;
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void Save()
    {
        SaveSettings();
        HasUnsavedChanges = false;
        ShowSaveConfirmation = true;
        _confirmationTimer?.Dispose();
        _confirmationTimer = new Timer(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowSaveConfirmation = false);
        }, null, 2000, Timeout.Infinite);
    }

    [RelayCommand]
    private void Close()
    {
        SaveSettings();
        RequestClose?.Invoke();
    }

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

    partial void OnShowTooltipsChanged(bool value) => HasUnsavedChanges = true;
    partial void OnShowNotificationsChanged(bool value) => HasUnsavedChanges = true;

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        var lang = value == 1 ? "pl" : "en";
        _loc.SetLanguage(lang);
        SaveSettings();

        // Refresh all key binding labels
        foreach (var row in PlaybackBindings.Concat(ViewBindings).Concat(ToolBindings).Concat(FileBindings).Concat(EditBindings))
            row.RefreshLabel();
    }

    partial void OnSelectedWindowModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsWindowedMode));
        OnPropertyChanged(nameof(IsMaximizedMode));
        OnPropertyChanged(nameof(IsFullscreenMode));
        HasUnsavedChanges = true;
    }

    public StartupWindowMode StartupWindowMode => (StartupWindowMode)SelectedWindowModeIndex;

    public KeyBindingSettings KeyBindings => _appSettings.KeyBindings;

    private void CheckForConflicts()
    {
        var allRows = PlaybackBindings
            .Concat(ViewBindings)
            .Concat(ToolBindings)
            .Concat(FileBindings)
            .Concat(EditBindings)
            .ToList();

        foreach (var row in allRows)
        {
            var duplicates = allRows.Where(r => r != row
                && r.Binding.Key == row.Binding.Key
                && r.Binding.Ctrl == row.Binding.Ctrl
                && r.Binding.Shift == row.Binding.Shift
                && r.Binding.Alt == row.Binding.Alt).ToList();

            row.HasConflict = duplicates.Count > 0;
            row.ConflictMessage = duplicates.Count > 0
                ? $"{_loc["Controls_ConflictsWith"]} {string.Join(", ", duplicates.Select(d => d.Label))}"
                : string.Empty;
        }
    }

    private void SaveSettings()
    {
        _appSettings.DefaultDirectory = DefaultDirectory;
        _appSettings.ShowTooltips = ShowTooltips;
        _appSettings.ShowNotifications = ShowNotifications;
        _appSettings.StartupWindowMode = StartupWindowMode;
        _appSettings.Language = SelectedLanguageIndex == 1 ? "pl" : "en";
        _settingsService.Save(_appSettings);
    }
}
