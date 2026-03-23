using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DicomViewer.ViewModels;
using DicomViewer.Controls;
using DicomViewer.Services;
using IconPacks.Avalonia.Codicons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DicomViewer.Views
{
    public partial class MainWindow : Window
    {
        private LogWindow? _logWindow;
        private KeyBindingSettings? _keyBindings;
        private LocalizationService Loc => LocalizationService.Instance;
        private readonly ImageService _imageService = new();
        private readonly VideoService _videoService = new();
        private readonly DicomService _dicomService = new();

        /// <summary>
        /// CancellationTokenSource for the current frame load operation.
        /// Cancelled whenever a new frame is requested (e.g., fast scrolling),
        /// so intermediate frames are skipped and only the final frame renders.
        /// </summary>
        private CancellationTokenSource? _frameLoadCts;
        private Task _currentRenderTask = Task.CompletedTask;

        public MainWindow()
        {
            InitializeComponent();

            // Title bar drag
            var dragArea = this.FindControl<Border>("TitleBarDragArea")!;
            dragArea.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(e);
            };

            // Window controls
            this.FindControl<Button>("BtnMinimise")!.Click += (_, _)
                => WindowState = WindowState.Minimized;

            var maxIcon = this.FindControl<PackIconCodicons>("MaximiseIcon")!;
            this.FindControl<Button>("BtnMaximise")!.Click += (_, _) =>
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                maxIcon.Kind = WindowState == WindowState.Maximized ? PackIconCodiconsKind.ChromeRestore : PackIconCodiconsKind.ChromeMaximize;
            };

            this.FindControl<Button>("BtnClose")!.Click += (_, _)
                => Close();

            this.FindControl<Button>("SettingsBtn")!.Click += async (_, _) => await OpenSettingsWindow();
            this.FindControl<Button>("LogViewerBtn")!.Click += (_, _) => OpenLogWindow();
            this.FindControl<Border>("StatusBar")!.PointerPressed += (_, _) => OpenLogWindow();

            // Drag & Drop
            AddHandler(DragDrop.DropEvent, OnDrop);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);

            KeyDown += OnKeyDown;

            // Wire up OpenFile dialog & re-render on property changes
            DataContextChanged += (s, e) =>
            {
                if (VM == null) return;

                VM.RequestOpenFile = async () =>
                {
                    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = Loc["Dialog_OpenFile"],
                        AllowMultiple = true,
                        FileTypeFilter = new List<FilePickerFileType>
                        {
                            new(Loc["Dialog_AllSupported"]) { Patterns = new[] { "*.dcm", "*.dicom", "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.tiff", "*.tif", "*.gif", "*.webp", "*.avi" } },
                            new(Loc["Dialog_DicomFiles"]) { Patterns = new[] { "*.dcm", "*.dicom" } },
                            new(Loc["Dialog_ImageFiles"]) { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.tiff", "*.tif", "*.gif", "*.webp" } },
                            new(Loc["Dialog_VideoFiles"]) { Patterns = new[] { "*.avi" } },
                            new(Loc["Dialog_AllFiles"]) { Patterns = new[] { "*.*" } }
                        }
                    });
                    if (files.Count > 0)
                    {
                        var paths = files.Select(f => f.TryGetLocalPath() ?? "").Where(p => !string.IsNullOrEmpty(p)).ToArray();
                        if (paths.Length > 0) await VM.OpenFilesFromPaths(paths);
                    }
                };

                VM.RequestOpenDirectory = async () =>
                {
                    var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = Loc["Dialog_OpenDirectory"],
                        AllowMultiple = false
                    });
                    if (folder.Count > 0)
                    {
                        var dirPath = folder[0].TryGetLocalPath();
                        if (!string.IsNullOrEmpty(dirPath))
                        {
                            // Show directory in browser tree
                            VM.LoadDirectoryTree(dirPath);
                            VM.IsRightPanelVisible = true;

                            // Also scan and load all DICOM files from the directory (recursive)
                            await OpenDicomFilesFromDirectoryRecursive(dirPath);
                        }
                    }
                };

                VM.RequestBrowseDirectory = async () =>
                {
                    var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = Loc["Dialog_SelectDirectory"],
                        AllowMultiple = false
                    });
                    if (folder.Count > 0)
                        return folder[0].TryGetLocalPath();
                    return null;
                };

                VM.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(MainWindowViewModel.ActiveFile) ||
                        args.PropertyName == nameof(MainWindowViewModel.CurrentFrameIndex))
                        _currentRenderTask = UpdateCanvasImageAsync();

                    if (args.PropertyName == nameof(MainWindowViewModel.CurrentFrameIndex))
                        ScrollFilmstripToCurrentFrame();

                    if (args.PropertyName == nameof(MainWindowViewModel.ClipboardText) && VM.ClipboardText != null)
                        _ = Clipboard?.SetTextAsync(VM.ClipboardText);
                };

                VM.WaitForFrameRenderAsync = () => _currentRenderTask;

                VM.PrefetchFramesAsync = (ct) =>
                {
                    if (VM?.ActiveFile == null) return Task.CompletedTask;
                    var model = VM.ActiveFile.Model;
                    int totalFrames = VM.TotalFrames;
                    var dicomService = _dicomService;

                    return Task.Run(() =>
                    {
                        for (int i = 0; i < totalFrames; i++)
                        {
                            if (ct.IsCancellationRequested) break;
                            var filePath = model.GetFilePathForFrame(i);
                            int frameIdx = model.IsStacked ? 0 : i;
                            // Skip non-DICOM files and already-cached frames
                            if (ImageService.IsSupported(filePath) || VideoService.IsSupported(filePath))
                                continue;
                            try
                            {
                                // LoadDicomPixels checks cache internally; no-op if already cached
                                dicomService.LoadDicomPixels(filePath, frameIdx, out _, out _, out _);
                            }
                            catch { /* Skip frames that fail to decode */ }
                        }
                    }, ct);
                };

                VM.GetCachedFrameCount = () => _dicomService.CachedFrameCount;

                VM.LoadSettings();
                ApplyStartupWindowMode(VM.StartupWindowMode);
            };
        }

        private MainWindowViewModel? VM => DataContext as MainWindowViewModel;

        // Each thumbnail is 74px wide + 3px margin on each side = 80px per item
        private const double ThumbItemWidth = 80.0;

        private void ScrollFilmstripToCurrentFrame()
        {
            var scroller = this.FindControl<ScrollViewer>("FilmstripScroller");
            if (scroller == null || VM == null) return;

            double targetOffset = VM.CurrentFrameIndex * ThumbItemWidth
                                  - scroller.Viewport.Width / 2
                                  + ThumbItemWidth / 2;

            double maxOffset = Math.Max(0, VM.TotalFrames * ThumbItemWidth - scroller.Viewport.Width);
            targetOffset = Math.Max(0, Math.Min(targetOffset, maxOffset));

            scroller.Offset = new Avalonia.Vector(targetOffset, 0);
        }

        /// <summary>
        /// Asynchronously loads and displays the current frame on the canvas.
        /// Cancels any in-flight frame load when a new frame is requested,
        /// so fast scrolling skips intermediate frames (e.g., JPEG 2000 at ~1.5s/frame).
        /// </summary>
        private async Task UpdateCanvasImageAsync()
        {
            if (VM?.ActiveFile == null) return;
            var canvas = this.FindControl<DicomCanvas>("MainCanvas");
            if (canvas == null) return;

            // PROBLEM 1 FIX: Clamp frame index to valid range before any rendering attempt
            int safeFrameIndex = Math.Clamp(VM.CurrentFrameIndex, 0, Math.Max(0, VM.TotalFrames - 1));
            if (safeFrameIndex != VM.CurrentFrameIndex)
            {
                LoggingService.Instance.Warning("Canvas",
                    $"Frame index {VM.CurrentFrameIndex} out of range (0-{VM.TotalFrames - 1}), clamped to {safeFrameIndex}");
                VM.CurrentFrameIndex = safeFrameIndex;
                return; // Setting CurrentFrameIndex will re-trigger this method
            }

            // Cancel any previous in-flight frame decode (fast-scroll skip)
            _frameLoadCts?.Cancel();
            _frameLoadCts?.Dispose();
            _frameLoadCts = new CancellationTokenSource();
            var ct = _frameLoadCts.Token;

            // PROBLEM 3 FIX: For stacked series, get the correct file path for this slice
            var model = VM.ActiveFile.Model;
            var filePath = model.GetFilePathForFrame(safeFrameIndex);
            // For stacked files, each slice is frame 0 of its own file
            int actualFrameIndex = model.IsStacked ? 0 : safeFrameIndex;

            try
            {
                if (ImageService.IsSupported(filePath))
                {
                    // Images are fast — load synchronously
                    var pixels = _imageService.LoadPixels(filePath, out int w, out int h);
                    if (ct.IsCancellationRequested) return;
                    canvas.SetPixels(pixels, w, h);
                }
                else if (VideoService.IsSupported(filePath))
                {
                    // Video frames are fast — load synchronously
                    var pixels = _videoService.LoadFrame(filePath, actualFrameIndex, out int w, out int h);
                    if (ct.IsCancellationRequested) return;
                    canvas.SetPixels(pixels, w, h);
                }
                else
                {
                    // DICOM: use async loading — heavy transcoding runs on background thread
                    VM.IsLoadingFrame = true;
                    var result = await _dicomService.LoadDicomPixelsAsync(filePath, actualFrameIndex, ct);

                    // Check cancellation after await — another frame may have been requested
                    if (ct.IsCancellationRequested) return;

                    if (VM.IsPlaying)
                    {
                        // During playback, build RGBA buffer on background thread
                        // to keep UI thread free for rendering
                        double wc = canvas.WindowCenter, ww = canvas.WindowWidth;
                        bool inv = canvas.IsInverted;
                        var rgba = await Task.Run(() =>
                            DicomCanvas.BuildRgbaBuffer(result.Pixels, result.Width, result.Height,
                                result.IsColor, wc, ww, inv), ct);
                        if (ct.IsCancellationRequested) return;
                        canvas.SetPrebuiltRgba(rgba, result.Pixels, result.Width, result.Height, result.IsColor);
                    }
                    else
                    {
                        canvas.SetPixels(result.Pixels, result.Width, result.Height, result.IsColor);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when fast-scrolling cancels intermediate frames — silently skip
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested) return; // Don't show errors for cancelled frames
                LoggingService.Instance.Error("Canvas", $"Failed to render frame {safeFrameIndex}", ex);
                VM.AddNotification(ViewModels.NotificationSeverity.Error,
                    $"{Loc["Notif_FailedRenderFrame"]} {safeFrameIndex}",
                    ex.Message);
            }
            finally
            {
                if (VM != null)
                    VM.IsLoadingFrame = false;
            }
        }

        // --- DRAG AND DROP HANDLERS ---
        private void OnDragOver(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                e.DragEffects = DragDropEffects.Copy;
                var overlay = this.FindControl<Border>("DropOverlay");
                if (overlay != null) overlay.IsVisible = true;
            }
        }

        private void OnDragLeave(object? sender, DragEventArgs e)
        {
            var overlay = this.FindControl<Border>("DropOverlay");
            if (overlay != null) overlay.IsVisible = false;
        }

        private async void OnDrop(object? sender, DragEventArgs e)
        {
            var overlay = this.FindControl<Border>("DropOverlay");
            if (overlay != null) overlay.IsVisible = false;

            if (VM == null) return;

            var files = e.Data.GetFiles();
            if (files != null)
            {
                var paths = files.Select(f => f.TryGetLocalPath() ?? "").Where(p => !string.IsNullOrEmpty(p)).ToArray();
                if (paths.Length > 0) await VM.OpenFilesFromPaths(paths);
            }
        }

        // --- TOOL SYNC HANDLERS ---
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            // Wire filmstrip scroll: vertical wheel → horizontal pan
            var filmstrip = this.FindControl<ScrollViewer>("FilmstripScroller");
            if (filmstrip != null)
            {
                filmstrip.AddHandler(PointerWheelChangedEvent, OnFilmstripWheel, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            }

            MainCanvas.ZoomLevelChanged += (_, z) => { if (VM != null) VM.ZoomLevel = z; };
            MainCanvas.PanChanged += (_, p) => { if (VM != null) { VM.PanX = p.X; VM.PanY = p.Y; } };
            MainCanvas.WindowLevelChanged += (_, wl) => { if (VM != null) { VM.WindowCenter = wl.Center; VM.WindowWidth = wl.Width; } };
            MainCanvas.FrameScrolled += (_, dir) => {
                if (VM != null)
                {
                    if (dir > 0) VM.NextFrameCommand.Execute(null);
                    else VM.PreviousFrameCommand.Execute(null);
                }
            };

            // Wire scroll-wheel on playback bar sliders
            var frameSlider = this.FindControl<Slider>("FrameSlider");
            if (frameSlider != null)
                frameSlider.AddHandler(PointerWheelChangedEvent, OnFrameSliderWheel, RoutingStrategies.Tunnel, handledEventsToo: true);

            var fpsSlider = this.FindControl<Slider>("FpsSlider");
            if (fpsSlider != null)
                fpsSlider.AddHandler(PointerWheelChangedEvent, OnFpsSliderWheel, RoutingStrategies.Tunnel, handledEventsToo: true);

            var wcSlider = this.FindControl<Slider>("WindowCenterSlider");
            if (wcSlider != null)
                wcSlider.AddHandler(PointerWheelChangedEvent, OnWindowCenterSliderWheel, RoutingStrategies.Tunnel, handledEventsToo: true);

            var wwSlider = this.FindControl<Slider>("WindowWidthSlider");
            if (wwSlider != null)
                wwSlider.AddHandler(PointerWheelChangedEvent, OnWindowWidthSliderWheel, RoutingStrategies.Tunnel, handledEventsToo: true);

            _keyBindings = new SettingsService().Load().KeyBindings;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (VM == null || _keyBindings == null) return;

            // Skip all single-letter shortcuts while canvas is in text editing mode
            if (MainCanvas.IsEditingText)
            {
                // Only allow Escape (to finish editing) and Ctrl+ combos while typing
                if (e.Key == Key.Escape)
                {
                    VM.SelectToolCommand.Execute("None");
                    e.Handled = true;
                }
                return;
            }

            var key = e.Key;
            var mod = e.KeyModifiers;

            // Playback
            if (Matches(_keyBindings.TogglePlay, key, mod)) VM.TogglePlayCommand.Execute(null);
            else if (Matches(_keyBindings.PreviousFrame, key, mod)) VM.PreviousFrameCommand.Execute(null);
            else if (Matches(_keyBindings.NextFrame, key, mod)) VM.NextFrameCommand.Execute(null);
            else if (Matches(_keyBindings.FirstFrame, key, mod)) VM.FirstFrameCommand.Execute(null);
            else if (Matches(_keyBindings.LastFrame, key, mod)) VM.LastFrameCommand.Execute(null);
            // View
            else if (Matches(_keyBindings.ZoomIn, key, mod) || (key == Key.Add && !_keyBindings.ZoomIn.Ctrl && !_keyBindings.ZoomIn.Shift && !_keyBindings.ZoomIn.Alt)) VM.ZoomInCommand.Execute(null);
            else if (Matches(_keyBindings.ZoomOut, key, mod) || (key == Key.Subtract && !_keyBindings.ZoomOut.Ctrl && !_keyBindings.ZoomOut.Shift && !_keyBindings.ZoomOut.Alt)) VM.ZoomOutCommand.Execute(null);
            else if (Matches(_keyBindings.FitToWindow, key, mod)) VM.FitToWindowCommand.Execute(null);
            else if (Matches(_keyBindings.ResetView, key, mod)) VM.ResetViewCommand.Execute(null);
            else if (Matches(_keyBindings.ToggleInvert, key, mod)) VM.ToggleInvertCommand.Execute(null);
            else if (Matches(_keyBindings.ToggleFullscreen, key, mod)) ToggleFullscreen();
            // File
            else if (Matches(_keyBindings.OpenFile, key, mod)) _ = VM.OpenFileCommand.ExecuteAsync(null);
            else if (Matches(_keyBindings.OpenLogs, key, mod)) OpenLogWindow();
            // Edit
            else if (Matches(_keyBindings.Undo, key, mod)) { MainCanvas.UndoAnnotation(); e.Handled = true; }
            else if (Matches(_keyBindings.Redo, key, mod)) { MainCanvas.RedoAnnotation(); e.Handled = true; }
            // Annotation tools
            else if (Matches(_keyBindings.ToolArrow, key, mod)) VM.SelectToolCommand.Execute("Arrow");
            else if (Matches(_keyBindings.ToolText, key, mod)) VM.SelectToolCommand.Execute("TextLabel");
            else if (Matches(_keyBindings.ToolFreehand, key, mod)) VM.SelectToolCommand.Execute("Freehand");
            else if (Matches(_keyBindings.CycleColor, key, mod)) VM.CycleAnnotationColorCommand.Execute(null);
            else if (Matches(_keyBindings.DeselectTool, key, mod)) VM.SelectToolCommand.Execute("None");
        }

        private static bool Matches(Services.KeyBinding binding, Key key, KeyModifiers modifiers)
        {
            if (binding.Key != key.ToString()) return false;
            if (binding.Ctrl != modifiers.HasFlag(KeyModifiers.Control)) return false;
            if (binding.Shift != modifiers.HasFlag(KeyModifiers.Shift)) return false;
            if (binding.Alt != modifiers.HasFlag(KeyModifiers.Alt)) return false;
            return true;
        }

        private void ApplyStartupWindowMode(StartupWindowMode mode)
        {
            switch (mode)
            {
                case StartupWindowMode.Maximized:
                    WindowState = WindowState.Maximized;
                    break;
                case StartupWindowMode.Fullscreen:
                    WindowState = WindowState.FullScreen;
                    _isFullscreen = true;
                    break;
            }
            var maxIcon = this.FindControl<PackIconCodicons>("MaximiseIcon");
            if (maxIcon != null)
                maxIcon.Kind = WindowState == WindowState.Maximized ? PackIconCodiconsKind.ChromeRestore : PackIconCodiconsKind.ChromeMaximize;
        }

        private bool _isFullscreen;
        private WindowState _preFullscreenState;

        private void ToggleFullscreen()
        {
            var maxIcon = this.FindControl<PackIconCodicons>("MaximiseIcon");
            if (_isFullscreen)
            {
                WindowState = _preFullscreenState;
                SystemDecorations = SystemDecorations.BorderOnly;
                ExtendClientAreaToDecorationsHint = true;
                _isFullscreen = false;
            }
            else
            {
                _preFullscreenState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState;
                WindowState = WindowState.FullScreen;
                _isFullscreen = true;
            }
            if (maxIcon != null)
                maxIcon.Kind = WindowState == WindowState.Maximized ? PackIconCodiconsKind.ChromeRestore : PackIconCodiconsKind.ChromeMaximize;
        }

        // ── Settings Window ──────────────────────────────────────────────────
        private async Task OpenSettingsWindow()
        {
            var vm = new ViewModels.SettingsViewModel();
            vm.OnLanguageChanged = (title, detail) =>
                VM?.AddNotification(NotificationSeverity.Info, title, detail);
            vm.RequestBrowseDirectory = async () =>
            {
                var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = Loc["Dialog_SelectDirectory"],
                    AllowMultiple = false
                });
                if (folder.Count > 0)
                    return folder[0].TryGetLocalPath();
                return null;
            };

            var settingsWindow = new SettingsWindow { DataContext = vm };
            await settingsWindow.ShowDialog(this);

            // Sync back to main VM
            if (VM != null)
            {
                VM.ShowTooltips = vm.ShowTooltips;

                if (VM.DefaultDirectory != vm.DefaultDirectory)
                {
                    VM.DefaultDirectory = vm.DefaultDirectory;
                    if (!string.IsNullOrEmpty(vm.DefaultDirectory) && System.IO.Directory.Exists(vm.DefaultDirectory))
                    {
                        VM.LoadDirectoryTree(vm.DefaultDirectory);
                        VM.IsRightPanelVisible = true;
                    }
                }
            }

            // Reload key bindings and settings so changes take effect immediately
            var reloaded = new SettingsService().Load();
            _keyBindings = reloaded.KeyBindings;
            VM?.ReloadAppSettings();
        }

        private void OpenLogWindow()
        {
            if (_logWindow != null && _logWindow.IsVisible)
            {
                _logWindow.Activate();
                return;
            }
            _logWindow = new LogWindow { DataContext = VM?.LogViewer };
            _logWindow.Closed += (_, _) => _logWindow = null;
            _logWindow.Show(this);
        }

        // ── Slider scroll-wheel handlers ─────────────────────────────────────
        private void OnFrameSliderWheel(object? sender, PointerWheelEventArgs e)
        {
            if (VM == null) return;
            if (e.Delta.Y > 0) VM.PreviousFrameCommand.Execute(null);
            else VM.NextFrameCommand.Execute(null);
            e.Handled = true;
        }

        private void OnFpsSliderWheel(object? sender, PointerWheelEventArgs e)
        {
            if (VM == null) return;
            VM.PlaybackFps = Math.Clamp(VM.PlaybackFps + (e.Delta.Y > 0 ? 1 : -1), 1, 60);
            e.Handled = true;
        }

        private void OnWindowCenterSliderWheel(object? sender, PointerWheelEventArgs e)
        {
            if (VM == null) return;
            VM.WindowCenter = Math.Clamp(VM.WindowCenter + (e.Delta.Y > 0 ? 10 : -10), 0, 65535);
            e.Handled = true;
        }

        private void OnWindowWidthSliderWheel(object? sender, PointerWheelEventArgs e)
        {
            if (VM == null) return;
            VM.WindowWidth = Math.Clamp(VM.WindowWidth + (e.Delta.Y > 0 ? 10 : -10), 1, 65535);
            e.Handled = true;
        }

        // ── Filmstrip vertical scroll → horizontal scroll ───────────────────
        private void OnFilmstripWheel(object? sender, PointerWheelEventArgs e)
        {
            var scroller = this.FindControl<ScrollViewer>("FilmstripScroller");
            if (scroller == null) return;

            double step = ThumbItemWidth * 3 * (e.Delta.Y > 0 ? -1 : 1);
            scroller.Offset = new Avalonia.Vector(
                Math.Clamp(scroller.Offset.X + step, 0, scroller.ScrollBarMaximum.X), 0);

            e.Handled = true;
        }

        // --- UI INTERACTION METHODS ---

        private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border b && b.DataContext is DicomFileViewModel fileVM && VM != null)
            {
                _ = VM.SelectFileCommand.ExecuteAsync(fileVM);
            }
        }

        private void OnFileListPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border b && b.DataContext is DicomFileViewModel fileVM && VM != null)
            {
                _ = VM.SelectFileCommand.ExecuteAsync(fileVM);
            }
        }

        private void OnThumbnailPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border b && b.DataContext is ThumbnailViewModel thumbVM && VM != null)
            {
                // Use FrameDisplayIndex (position in thumbnail list) for stacked series,
                // since FrameIndex is always 0 for stacked (each file has one frame)
                int idx = thumbVM.FrameDisplayIndex >= 0 ? thumbVM.FrameDisplayIndex : thumbVM.FrameIndex;
                VM.CurrentFrameIndex = Math.Clamp(idx, 0, Math.Max(0, VM.TotalFrames - 1));
            }
        }

        private void OnBrowserCollapsePressed(object? sender, PointerPressedEventArgs e)
        {
            if (VM != null) VM.IsBrowserExpanded = !VM.IsBrowserExpanded;
        }

        private void OnFilmstripHandlePressed(object? sender, PointerPressedEventArgs e)
        {
            if (VM != null) VM.ShowMiniFrames = !VM.ShowMiniFrames;
        }

        private void OnTreeNodePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border b && b.DataContext is FileTreeNodeViewModel node && VM != null)
            {
                if (node.IsDirectory)
                {
                    // Toggle expand/collapse
                    node.IsExpanded = !node.IsExpanded;

                    // Also load all DICOM files from this folder as a stacked series
                    _ = OpenDicomFilesFromDirectory(node.FullPath);
                }
                else
                {
                    // For individual file clicks, gather ALL sibling DICOM files
                    // from the same directory to enable series stacking
                    var dir = System.IO.Path.GetDirectoryName(node.FullPath);
                    if (dir != null)
                    {
                        var dicomFiles = System.IO.Directory.GetFiles(dir)
                            .Where(f =>
                            {
                                var ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                                return ext is ".dcm" or ".dicom";
                            })
                            .OrderBy(f => f)
                            .ToArray();

                        if (dicomFiles.Length > 1)
                        {
                            // Multiple DICOM files in same folder → load all for stacking
                            _ = VM.OpenFilesFromPaths(dicomFiles);
                        }
                        else
                        {
                            // Single file or non-DICOM → load individually
                            _ = VM.OpenFilesFromPaths(new[] { node.FullPath });
                        }
                    }
                    else
                    {
                        _ = VM.OpenFilesFromPaths(new[] { node.FullPath });
                    }
                }
            }
        }

        /// <summary>
        /// Scans a directory for DICOM files and loads them via OpenFilesFromPaths
        /// which will auto-group them into series stacks.
        /// </summary>
        private async Task OpenDicomFilesFromDirectory(string dirPath)
        {
            if (VM == null) return;

            try
            {
                var dicomFiles = System.IO.Directory.GetFiles(dirPath)
                    .Where(f =>
                    {
                        var ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                        return ext is ".dcm" or ".dicom";
                    })
                    .OrderBy(f => f)
                    .ToArray();

                if (dicomFiles.Length > 0)
                {
                    LoggingService.Instance.Info("FolderOpen",
                        $"Opening {dicomFiles.Length} DICOM files from {System.IO.Path.GetFileName(dirPath)}");
                    await VM.OpenFilesFromPaths(dicomFiles);
                }
                else
                {
                    LoggingService.Instance.Debug("FolderOpen",
                        $"No DICOM files found in {System.IO.Path.GetFileName(dirPath)}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warning("FolderOpen",
                    $"Error scanning directory: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively scans a directory tree for DICOM files and loads them.
        /// Groups files by series for stacking.
        /// </summary>
        private async Task OpenDicomFilesFromDirectoryRecursive(string dirPath)
        {
            if (VM == null) return;

            try
            {
                var dicomFiles = System.IO.Directory.GetFiles(dirPath, "*.*", System.IO.SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        var ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                        return ext is ".dcm" or ".dicom";
                    })
                    .OrderBy(f => f)
                    .ToArray();

                if (dicomFiles.Length > 0)
                {
                    LoggingService.Instance.Info("FolderOpen",
                        $"Found {dicomFiles.Length} DICOM files in {System.IO.Path.GetFileName(dirPath)} (recursive)");
                    await VM.OpenFilesFromPaths(dicomFiles);
                }
                else
                {
                    LoggingService.Instance.Info("FolderOpen",
                        $"No DICOM files found in {System.IO.Path.GetFileName(dirPath)}");
                    VM.AddNotification(ViewModels.NotificationSeverity.Info,
                        Loc["Notif_NoDicomInDir"]);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warning("FolderOpen",
                    $"Error scanning directory recursively: {ex.Message}");
            }
        }
    } // End of MainWindow class
} // End of namespace