using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DicomViewer.Models;
using DicomViewer.Services;
using Avalonia.Threading;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DicomViewer.ViewModels;

public enum MouseTool { None, Pan, WindowLevel, Arrow, TextLabel, Freehand, DrawRect, DrawEllipse, DrawLine }

public partial class MainWindowViewModel : ViewModelBase
{
    public Func<Task>? RequestOpenFile { get; set; }
    public Func<Task>? RequestOpenDirectory { get; set; }
    public Func<Task<string?>>? RequestBrowseDirectory { get; set; }

    private readonly SettingsService _settingsService = new();
    private readonly LoggingService _log = LoggingService.Instance;
    private AppSettings _appSettings = new();

    [ObservableProperty] private string _defaultDirectory = string.Empty;
    [ObservableProperty] private bool _showTooltips = true;
    [ObservableProperty] private StartupWindowMode _startupWindowMode = StartupWindowMode.Windowed;
    [ObservableProperty] private bool _isSettingsOpen;

    [ObservableProperty] private MouseTool _activeTool = MouseTool.None;
    [ObservableProperty] private bool _toolPan;
    [ObservableProperty] private bool _toolWindowLevel;

    // Annotation color index into AnnotationColors.All
    [ObservableProperty] private int _annotationColorIndex;
    [ObservableProperty] private double _annotationStrokeWidth = 2.0;
    [ObservableProperty] private double _annotationFontSize = 14.0;
    [ObservableProperty] private bool _showAnnotations = true;

    [ObservableProperty] private double _zoomLevel = 1.0;
    [ObservableProperty] private double _panX;
    [ObservableProperty] private double _panY;
    [ObservableProperty] private double _rotation;
    [ObservableProperty] private bool _isFlippedH;
    [ObservableProperty] private bool _isFlippedV;
    [ObservableProperty] private bool _invertColors;

    [ObservableProperty] private double _windowCenter = 32768;
    [ObservableProperty] private double _windowWidth = 65535;

    [ObservableProperty] private int _currentFrameIndex;
    [ObservableProperty] private int _totalFrames = 1;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private int _playbackFps = 10;
    [ObservableProperty] private bool _loopPlayback = true;

    private CancellationTokenSource? _playCts;

    [ObservableProperty] private bool _isRightPanelVisible = true;
    [ObservableProperty] private bool _isBrowserExpanded = true;
    [ObservableProperty] private bool _isLoadingFile;
    [ObservableProperty] private double _loadingProgress;
    [ObservableProperty] private string _statusMessage = "Ready - Open a DICOM file to begin";
    [ObservableProperty] private DicomFileViewModel? _activeFile;
    [ObservableProperty] private string _activeFileInfo = string.Empty;

    [ObservableProperty] private bool _showOverlay = true;
    [ObservableProperty] private bool _showMiniFrames = false;

    // True when more than one file is open — drives tab bar visibility
    public bool HasMultipleFiles => OpenFiles.Count > 1;

    // 1-based frame number for display
    public int CurrentFrameDisplay => CurrentFrameIndex + 1;

    public ObservableCollection<DicomFileViewModel> OpenFiles { get; } = new();
    public ObservableCollection<ThumbnailViewModel> Thumbnails { get; } = new();
    public ObservableCollection<FileTreeNodeViewModel> DirectoryTree { get; } = new();
    public ObservableCollection<NotificationViewModel> Notifications { get; } = new();

    private const int MaxVisibleNotifications = 3;

    public void AddNotification(NotificationSeverity severity, string message, string details = "")
    {
        var notification = NotificationViewModel.Create(severity, message, details);

        Dispatcher.UIThread.Post(() =>
        {
            Notifications.Insert(0, notification);

            // Trim excess notifications
            while (Notifications.Count > MaxVisibleNotifications)
                Notifications.RemoveAt(Notifications.Count - 1);

            // Schedule auto-dismiss if applicable
            if (notification.AutoDismissMs > 0)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(notification.AutoDismissMs);
                    Dispatcher.UIThread.Post(() => DismissNotification(notification));
                });
            }
        });
    }

    [RelayCommand]
    private void DismissNotification(NotificationViewModel? notification)
    {
        if (notification != null)
            Notifications.Remove(notification);
    }

    [RelayCommand]
    private void CopyNotificationDetails(NotificationViewModel? notification)
    {
        if (notification == null) return;
        var text = string.IsNullOrEmpty(notification.Details)
            ? notification.Message
            : $"{notification.Message}\n\n{notification.Details}";
        _clipboardText = text;
        OnPropertyChanged(nameof(ClipboardText));
    }

    // Exposed so the View can copy to clipboard (Avalonia clipboard requires TopLevel)
    private string? _clipboardText;
    public string? ClipboardText => _clipboardText;

    [ObservableProperty] private bool _hasDirectoryLoaded;

    public void LoadDirectoryTree(string dirPath)
    {
        DirectoryTree.Clear();
        var root = new FileTreeNodeViewModel(dirPath, true) { IsExpanded = true };
        DirectoryTree.Add(root);
        HasDirectoryLoaded = true;
    }

    [RelayCommand]
    private void SelectTool(string tool)
    {
        var parsed = Enum.Parse<MouseTool>(tool, true);

        // Clicking the already-active tool deactivates it
        if (ActiveTool == parsed)
        {
            ToolPan = ToolWindowLevel = false;
            ActiveTool = MouseTool.None;
            StatusMessage = "No tool selected — scroll to navigate frames";
            return;
        }

        ToolPan = ToolWindowLevel = false;
        ActiveTool = parsed;
        StatusMessage = ActiveTool switch
        {
            MouseTool.Pan         => "Pan / Zoom — drag to pan, scroll to zoom",
            MouseTool.WindowLevel => "Window/Level — drag left/right: center, up/down: width",
            MouseTool.Arrow       => "Arrow — click and drag to point at structures",
            MouseTool.TextLabel   => "Text — click to place a text label",
            MouseTool.Freehand    => "Freehand — draw freely on the image",
            MouseTool.DrawRect    => "Rectangle — click and drag to draw a rectangle",
            MouseTool.DrawEllipse => "Ellipse — click and drag to draw an ellipse",
            MouseTool.DrawLine    => "Line — click and drag to draw a line",
            _ => "No tool selected"
        };
        ToolPan = ActiveTool == MouseTool.Pan;
        ToolWindowLevel = ActiveTool == MouseTool.WindowLevel;
    }

    [RelayCommand]
    private void CycleAnnotationColor()
    {
        AnnotationColorIndex = (AnnotationColorIndex + 1) % AnnotationColors.All.Length;
        StatusMessage = $"Annotation color: {AnnotationColors.Names[AnnotationColorIndex]}";
    }

    [RelayCommand]
    private void SetAnnotationColor(string indexStr)
    {
        if (int.TryParse(indexStr, out int idx) && idx >= 0 && idx < AnnotationColors.All.Length)
            AnnotationColorIndex = idx;
    }

    [RelayCommand]
    private void ToggleAnnotations() => ShowAnnotations = !ShowAnnotations;

    [RelayCommand]
    private async Task OpenFile()
    {
        if (RequestOpenFile != null)
            await RequestOpenFile();
    }

    [RelayCommand]
    private async Task OpenDirectory()
    {
        if (RequestOpenDirectory != null)
            await RequestOpenDirectory();
    }

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

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
                LoadDirectoryTree(dir);
                IsRightPanelVisible = true;
            }
        }
    }

    [RelayCommand]
    private void ClearDefaultDirectory()
    {
        DefaultDirectory = string.Empty;
        SaveSettings();
    }

    public LogViewerViewModel LogViewer { get; } = new();

    [ObservableProperty] private string? _errorStatusMessage;
    [ObservableProperty] private bool _hasStatusError;

    [RelayCommand]
    private void DismissStatus() { HasStatusError = false; ErrorStatusMessage = null; }

    public void LoadSettings()
    {
        _log.LogAdded += entry =>
        {
            if (entry.Level >= LogLevel.Warning)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    ErrorStatusMessage = $"[{entry.LevelLabel}] {entry.Category}: {entry.Message}";
                    HasStatusError = true;
                });
            }
        };

        _log.Info("App", "DicomViewer starting up");
        _appSettings = _settingsService.Load();
        DefaultDirectory = _appSettings.DefaultDirectory;
        ShowTooltips = _appSettings.ShowTooltips;
        StartupWindowMode = _appSettings.StartupWindowMode;

        if (!string.IsNullOrEmpty(DefaultDirectory) && System.IO.Directory.Exists(DefaultDirectory))
        {
            LoadDirectoryTree(DefaultDirectory);
            IsRightPanelVisible = true;
        }
    }

    private void SaveSettings()
    {
        _appSettings.DefaultDirectory = DefaultDirectory;
        _settingsService.Save(_appSettings);
    }

    public async Task OpenFilesFromPaths(string[] paths)
    {
        foreach (var path in paths)
            await LoadFileAsync(path);
    }

    private async Task LoadFileAsync(string path)
    {
        if (OpenFiles.Any(f => f.FilePath == path)) return;

        IsLoadingFile = true;
        LoadingProgress = 0;
        StatusMessage = $"Opening: {System.IO.Path.GetFileName(path)}";

        try
        {
            _log.Info("FileOpen", $"Opening file: {System.IO.Path.GetFileName(path)}");
            LoadingProgress = 15;
            StatusMessage = "Reading DICOM headers...";
            await Task.Delay(50);

            var vm = await Task.Run(() => DicomFileViewModel.Create(path));

            LoadingProgress = 60;
            StatusMessage = $"Parsing metadata - {vm.TotalFrames} frame(s) found...";
            await Task.Delay(50);

            OpenFiles.Add(vm);
            OnPropertyChanged(nameof(HasMultipleFiles)); // update tab bar visibility

            LoadingProgress = 80;
            StatusMessage = "Building thumbnails...";
            await Task.Delay(30);

            await SelectFile(vm);

            // Auto-show filmstrip once a file with frames is loaded
            if (vm.TotalFrames > 1)
                ShowMiniFrames = true;

            _log.Info("FileOpen", $"Loaded {vm.DisplayName} ({vm.TotalFrames} frames)");
            LoadingProgress = 100;
            StatusMessage = $"Ready - {vm.DisplayName}";
            await Task.Delay(300);
        }
        catch (Exception ex)
        {
            _log.Error("FileOpen", $"Failed to open {System.IO.Path.GetFileName(path)}", ex);
            LoadingProgress = 0;
            StatusMessage = $"Error: {ex.Message}";
            AddNotification(NotificationSeverity.Error,
                $"Failed to open {System.IO.Path.GetFileName(path)}",
                $"{ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            IsLoadingFile = false;
            LoadingProgress = 0;
        }
    }

    /// <summary>
    /// Called from XAML tab click or file list click to switch the active file.
    /// </summary>
    partial void OnCurrentFrameIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentFrameDisplay));
        UpdateThumbnailSelection();
    }

    [RelayCommand]
    public async Task SelectFile(DicomFileViewModel? file)
    {
        if (file == null) return;
        StopPlayback();
        foreach (var f in OpenFiles) f.IsSelected = false;
        file.IsSelected = true;
        ActiveFile = file;
        TotalFrames = file.TotalFrames;
        CurrentFrameIndex = 0;
        WindowCenter = file.Model.WindowCenter;
        WindowWidth = file.Model.WindowWidth;
        ActiveFileInfo = $"{file.PatientName}  |  {file.Modality}  |  {file.StudyDate}  |  {file.TotalFrames} frames";
        BuildThumbnails(file);
        ZoomLevel = 1.0; PanX = 0; PanY = 0; Rotation = 0;
        await Task.CompletedTask;
    }

    private void BuildThumbnails(DicomFileViewModel file)
    {
        Thumbnails.Clear();
        // Cap at 200 frames for performance; thumbnails load async so this is safe
        var count = Math.Min(file.TotalFrames, 200);
        for (int i = 0; i < count; i++)
            Thumbnails.Add(new ThumbnailViewModel(i, file.FilePath, i == 0));
    }

    [RelayCommand]
    private void CloseFile(DicomFileViewModel? file)
    {
        if (file == null) return;
        OpenFiles.Remove(file);
        OnPropertyChanged(nameof(HasMultipleFiles));

        if (file == ActiveFile)
        {
            ActiveFile = OpenFiles.FirstOrDefault();
            if (ActiveFile != null) _ = SelectFile(ActiveFile);
            else
            {
                Thumbnails.Clear();
                TotalFrames = 1;
                CurrentFrameIndex = 0;
                StatusMessage = "Ready - Open a DICOM file to begin";
            }
        }
    }

    [RelayCommand]
    private void TogglePlay()
    {
        if (IsPlaying) StopPlayback();
        else StartPlayback();
    }

    [RelayCommand]
    private void PreviousFrame()
    {
        CurrentFrameIndex = CurrentFrameIndex > 0
            ? CurrentFrameIndex - 1
            : (LoopPlayback ? TotalFrames - 1 : 0);
    }

    [RelayCommand]
    private void NextFrame()
    {
        CurrentFrameIndex = CurrentFrameIndex < TotalFrames - 1
            ? CurrentFrameIndex + 1
            : (LoopPlayback ? 0 : TotalFrames - 1);
    }

    [RelayCommand] private void FirstFrame() { CurrentFrameIndex = 0; }
    [RelayCommand] private void LastFrame() { CurrentFrameIndex = TotalFrames - 1; }

    private void StartPlayback()
    {
        if (TotalFrames <= 1) return;
        IsPlaying = true;
        _playCts = new CancellationTokenSource();
        var token = _playCts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1000 / Math.Max(1, PlaybackFps), token);
                if (token.IsCancellationRequested) break;
                Avalonia.Threading.Dispatcher.UIThread.Post(NextFrame);
            }
        }, token);
        StatusMessage = $"Playing - {PlaybackFps} FPS";
    }

    private void StopPlayback()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        _playCts?.Cancel();
        _playCts = null;
        StatusMessage = "Paused";
    }

    private void UpdateThumbnailSelection()
    {
        foreach (var t in Thumbnails)
            t.IsCurrentFrame = t.FrameIndex == CurrentFrameIndex;
    }

    [RelayCommand] private void ZoomIn() => ZoomLevel = Math.Min(ZoomLevel * 1.25, 20.0);
    [RelayCommand] private void ZoomOut() => ZoomLevel = Math.Max(ZoomLevel / 1.25, 0.05);
    [RelayCommand] private void FitToWindow() { ZoomLevel = 1.0; PanX = 0; PanY = 0; }
    [RelayCommand] private void ResetView() { ZoomLevel = 1.0; PanX = 0; PanY = 0; Rotation = 0; IsFlippedH = false; IsFlippedV = false; InvertColors = false; }
    [RelayCommand] private void RotateCW() => Rotation = (Rotation + 90) % 360;
    [RelayCommand] private void RotateCCW() => Rotation = (Rotation - 90 + 360) % 360;
    [RelayCommand] private void FlipH() => IsFlippedH = !IsFlippedH;
    [RelayCommand] private void FlipV() => IsFlippedV = !IsFlippedV;
    [RelayCommand] private void ToggleInvert() => InvertColors = !InvertColors;

    [RelayCommand]
    private void ApplyWindowPreset(string preset)
    {
        (WindowCenter, WindowWidth) = preset switch
        {
            "Lung" => (-600.0, 1500.0),
            "Bone" => (400.0, 1800.0),
            "Brain" => (40.0, 80.0),
            "Abdomen" => (60.0, 400.0),
            "Mediastinum" => (40.0, 400.0),
            "Liver" => (60.0, 150.0),
            _ => (40.0, 400.0)
        };
        StatusMessage = $"Preset: {preset}  C={WindowCenter} W={WindowWidth}";
    }

    [RelayCommand] private void ToggleRightPanel() => IsRightPanelVisible = !IsRightPanelVisible;
    [RelayCommand] private void ToggleBrowserExpanded() => IsBrowserExpanded = !IsBrowserExpanded;
    [RelayCommand] private void ToggleOverlay() => ShowOverlay = !ShowOverlay;
    [RelayCommand] private void ToggleMiniFrames() => ShowMiniFrames = !ShowMiniFrames;
}