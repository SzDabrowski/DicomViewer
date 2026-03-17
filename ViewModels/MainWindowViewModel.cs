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
    [ObservableProperty] private string _statusMessage = "";

    private readonly LocalizationService _loc = LocalizationService.Instance;
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
            StatusMessage = _loc["StatusNoTool"];
            return;
        }

        ToolPan = ToolWindowLevel = false;
        ActiveTool = parsed;
        StatusMessage = ActiveTool switch
        {
            MouseTool.Pan         => _loc["StatusPanZoom"],
            MouseTool.WindowLevel => _loc["StatusWindowLevel"],
            MouseTool.Arrow       => _loc["StatusArrow"],
            MouseTool.TextLabel   => _loc["StatusText"],
            MouseTool.Freehand    => _loc["StatusFreehand"],
            MouseTool.DrawRect    => _loc["StatusRectangle"],
            MouseTool.DrawEllipse => _loc["StatusEllipse"],
            MouseTool.DrawLine    => _loc["StatusLine"],
            _ => _loc["StatusNoToolSelected"]
        };
        ToolPan = ActiveTool == MouseTool.Pan;
        ToolWindowLevel = ActiveTool == MouseTool.WindowLevel;
    }

    [RelayCommand]
    private void CycleAnnotationColor()
    {
        AnnotationColorIndex = (AnnotationColorIndex + 1) % AnnotationColors.All.Length;
        StatusMessage = $"{_loc["AnnotationColor"]} {_loc[AnnotationColors.Names[AnnotationColorIndex]]}";
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
    private bool _logSubscribed;

    [RelayCommand]
    private void DismissStatus() { HasStatusError = false; ErrorStatusMessage = null; }

    private void OnLogEntryAdded(LogEntry entry)
    {
        if (entry.Level >= LogLevel.Warning)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ErrorStatusMessage = $"[{entry.LevelLabel}] {entry.Category}: {entry.Message}";
                HasStatusError = true;
            });
        }
    }

    public void LoadSettings()
    {
        if (!_logSubscribed)
        {
            _log.LogAdded += OnLogEntryAdded;
            _logSubscribed = true;
        }

        _log.Info("App", "DicomViewer starting up");
        _appSettings = _settingsService.Load();
        DefaultDirectory = _appSettings.DefaultDirectory;
        ShowTooltips = _appSettings.ShowTooltips;
        StartupWindowMode = _appSettings.StartupWindowMode;

        // Initialize language
        _loc.SetLanguage(_appSettings.Language);
        StatusMessage = _loc["StatusReady"];

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
        // PROBLEM 3 FIX: If multiple DICOM files are provided, try to group them into series stacks
        if (paths.Length > 1)
        {
            var dicomPaths = paths.Where(p =>
            {
                var ext = System.IO.Path.GetExtension(p).ToLowerInvariant();
                return !ImageService.IsSupported(p) && !VideoService.IsSupported(p);
            }).ToArray();

            var nonDicomPaths = paths.Except(dicomPaths).ToArray();

            if (dicomPaths.Length > 1)
            {
                try
                {
                    var dicomService = new Services.DicomService();
                    var stacks = dicomService.GroupFilesIntoStacks(dicomPaths);

                    foreach (var stack in stacks)
                    {
                        if (stack.SliceCount > 1)
                        {
                            // Load as a virtual stacked series
                            await LoadStackedSeriesAsync(stack);
                        }
                        else
                        {
                            // Single file in series — load normally
                            await LoadFileAsync(stack.FilePaths[0]);
                        }
                    }

                    // Load non-DICOM files normally
                    foreach (var path in nonDicomPaths)
                        await LoadFileAsync(path);

                    return;
                }
                catch (Exception ex)
                {
                    _log.Warning("Stacking", $"Series grouping failed, loading files individually: {ex.Message}");
                }
            }
        }

        // Fallback: load each file individually
        foreach (var path in paths)
            await LoadFileAsync(path);
    }

    private async Task LoadStackedSeriesAsync(Models.DicomSeriesStack stack)
    {
        if (OpenFiles.Any(f => f.Model.StackFilePaths != null &&
            f.Model.StackFilePaths.SequenceEqual(stack.FilePaths))) return;

        IsLoadingFile = true;
        LoadingProgress = 0;
        StatusMessage = $"{_loc["Opening"]} {stack.SeriesDescription} ({stack.SliceCount} slices)";

        try
        {
            _log.Info("Stacking", $"Loading stacked series: {stack.SeriesDescription} ({stack.SliceCount} slices)");
            LoadingProgress = 15;

            // Use the first file for metadata
            var firstPath = stack.FilePaths[0];
            var vm = await Task.Run(() =>
            {
                var fileVm = DicomFileViewModel.Create(firstPath);
                // Override the model to represent the full stack
                fileVm.Model.StackFilePaths = stack.FilePaths;
                fileVm.Model.TotalFrames = stack.SliceCount;
                fileVm.Model.SeriesDescription = string.IsNullOrEmpty(stack.SeriesDescription)
                    ? $"Series ({stack.SliceCount} slices)"
                    : $"{stack.SeriesDescription} ({stack.SliceCount} slices)";
                return fileVm;
            });

            LoadingProgress = 60;
            StatusMessage = $"{_loc["ParsingMetadata"]} - {stack.SliceCount} {_loc["FramesFound"]}";

            OpenFiles.Add(vm);
            OnPropertyChanged(nameof(HasMultipleFiles));

            LoadingProgress = 80;
            StatusMessage = _loc["BuildingThumbnails"];

            await SelectFile(vm);
            ShowMiniFrames = true;

            _log.Info("Stacking", $"Loaded stacked series: {vm.DisplayName} ({stack.SliceCount} slices)");
            LoadingProgress = 100;
            StatusMessage = $"{_loc["Ready"]} - {vm.DisplayName}";
        }
        catch (Exception ex)
        {
            _log.Error("Stacking", $"Failed to load series stack: {stack.SeriesDescription}", ex);
            StatusMessage = $"{_loc["Error"]} {ex.Message}";
            AddNotification(NotificationSeverity.Error,
                $"{_loc["Err_FailedToOpen"]} {stack.SeriesDescription}",
                $"{ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            IsLoadingFile = false;
            LoadingProgress = 0;
        }
    }

    private async Task LoadFileAsync(string path)
    {
        if (OpenFiles.Any(f => f.FilePath == path)) return;

        IsLoadingFile = true;
        LoadingProgress = 0;
        StatusMessage = $"{_loc["Opening"]} {System.IO.Path.GetFileName(path)}";

        try
        {
            _log.Info("FileOpen", $"Opening file: {System.IO.Path.GetFileName(path)}");
            LoadingProgress = 15;
            StatusMessage = _loc["ReadingHeaders"];

            var vm = await Task.Run(() => DicomFileViewModel.Create(path));

            LoadingProgress = 60;
            StatusMessage = $"{_loc["ParsingMetadata"]} - {vm.TotalFrames} {_loc["FramesFound"]}";

            OpenFiles.Add(vm);
            OnPropertyChanged(nameof(HasMultipleFiles)); // update tab bar visibility

            LoadingProgress = 80;
            StatusMessage = _loc["BuildingThumbnails"];

            await SelectFile(vm);

            // Auto-show filmstrip once a file with frames is loaded
            if (vm.TotalFrames > 1)
                ShowMiniFrames = true;

            _log.Info("FileOpen", $"Loaded {vm.DisplayName} ({vm.TotalFrames} frames)");
            LoadingProgress = 100;
            StatusMessage = $"{_loc["Ready"]} - {vm.DisplayName}";
        }
        catch (Exception ex)
        {
            _log.Error("FileOpen", $"Failed to open {System.IO.Path.GetFileName(path)}", ex);
            LoadingProgress = 0;
            StatusMessage = $"{_loc["Error"]} {ex.Message}";
            AddNotification(NotificationSeverity.Error,
                $"{_loc["Err_FailedToOpen"]} {System.IO.Path.GetFileName(path)}",
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

        // CRITICAL: Reset frame index BEFORE setting ActiveFile to prevent
        // out-of-range access when switching from multi-frame to fewer-frame file
        _currentFrameIndex = 0; // direct field write to avoid triggering canvas update
        TotalFrames = file.TotalFrames;
        OnPropertyChanged(nameof(CurrentFrameIndex));
        OnPropertyChanged(nameof(CurrentFrameDisplay));

        ActiveFile = file;
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
        {
            // PROBLEM 3: For stacked series, each thumbnail uses a different file (frame 0 of each)
            string thumbPath = file.Model.GetFilePathForFrame(i);
            int thumbFrame = file.Model.IsStacked ? 0 : i;
            Thumbnails.Add(new ThumbnailViewModel(thumbFrame, thumbPath, i == 0) { FrameDisplayIndex = i });
        }
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
                StatusMessage = _loc["StatusReady"];
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
        StatusMessage = $"{_loc["Playing"]} - {PlaybackFps} {_loc["FPS"]}";
    }

    private void StopPlayback()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        _playCts?.Cancel();
        _playCts = null;
        StatusMessage = _loc["Paused"];
    }

    private void UpdateThumbnailSelection()
    {
        foreach (var t in Thumbnails)
        {
            // For stacked series, compare against the display index
            int compareIdx = t.FrameDisplayIndex >= 0 ? t.FrameDisplayIndex : t.FrameIndex;
            t.IsCurrentFrame = compareIdx == CurrentFrameIndex;
        }
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
        // Preset values are in Hounsfield Units (modality space)
        var (huCenter, huWidth) = preset switch
        {
            "Lung" => (-600.0, 1500.0),
            "Bone" => (400.0, 1800.0),
            "Brain" => (40.0, 80.0),
            "Abdomen" => (60.0, 400.0),
            "Mediastinum" => (40.0, 400.0),
            "Liver" => (60.0, 150.0),
            "SoftTissue" => (40.0, 350.0),
            "Stroke" => (40.0, 40.0),
            "Spine" => (250.0, 1500.0),
            "Angio" => (300.0, 600.0),
            "ChestWide" => (-400.0, 1500.0),
            _ => (40.0, 400.0)
        };

        // Convert from HU to normalized 0-65535 pixel space using the active file's modality range
        if (ActiveFile?.Model != null)
        {
            WindowCenter = ActiveFile.Model.ModalityToNormalizedCenter(huCenter);
            WindowWidth = ActiveFile.Model.ModalityToNormalizedWidth(huWidth);
        }
        else
        {
            // No file loaded — use raw values as fallback
            WindowCenter = huCenter;
            WindowWidth = huWidth;
        }

        StatusMessage = $"{_loc["Preset"]} {_loc[preset]}  C={huCenter:F0} W={huWidth:F0} (HU)";
    }

    [RelayCommand] private void ToggleRightPanel() => IsRightPanelVisible = !IsRightPanelVisible;
    [RelayCommand] private void ToggleBrowserExpanded() => IsBrowserExpanded = !IsBrowserExpanded;
    [RelayCommand] private void ToggleOverlay() => ShowOverlay = !ShowOverlay;
    [RelayCommand] private void ToggleMiniFrames() => ShowMiniFrames = !ShowMiniFrames;
}