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
using System.Threading.Tasks;

namespace DicomViewer.Views
{
    public partial class MainWindow : Window
    {
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
                        Title = "Open File",
                        AllowMultiple = true,
                        FileTypeFilter = new List<FilePickerFileType>
                        {
                            new("All Supported") { Patterns = new[] { "*.dcm", "*.dicom", "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.tiff", "*.tif", "*.gif", "*.webp", "*.avi" } },
                            new("DICOM Files") { Patterns = new[] { "*.dcm", "*.dicom" } },
                            new("Image Files") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.tiff", "*.tif", "*.gif", "*.webp" } },
                            new("Video Files") { Patterns = new[] { "*.avi" } },
                            new("All Files") { Patterns = new[] { "*.*" } }
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
                        Title = "Open Directory",
                        AllowMultiple = false
                    });
                    if (folder.Count > 0)
                    {
                        var dirPath = folder[0].TryGetLocalPath();
                        if (!string.IsNullOrEmpty(dirPath))
                        {
                            VM.LoadDirectoryTree(dirPath);
                            VM.IsRightPanelVisible = true;
                        }
                    }
                    await Task.CompletedTask;
                };

                VM.RequestBrowseDirectory = async () =>
                {
                    var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "Select Default Directory",
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
                        UpdateCanvasImage();

                    if (args.PropertyName == nameof(MainWindowViewModel.CurrentFrameIndex))
                        ScrollFilmstripToCurrentFrame();

                    if (args.PropertyName == nameof(MainWindowViewModel.ClipboardText) && VM.ClipboardText != null)
                        _ = Clipboard?.SetTextAsync(VM.ClipboardText);
                };

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

        private void UpdateCanvasImage()
        {
            if (VM?.ActiveFile == null) return;
            var canvas = this.FindControl<DicomCanvas>("MainCanvas");
            if (canvas == null) return;
            var filePath = VM.ActiveFile.FilePath;

            try
            {
                if (ImageService.IsSupported(filePath))
                {
                    var imgSvc = new ImageService();
                    var pixels = imgSvc.LoadPixels(filePath, out int w, out int h);
                    canvas.SetPixels(pixels, w, h);
                }
                else if (VideoService.IsSupported(filePath))
                {
                    var vidSvc = new VideoService();
                    var pixels = vidSvc.LoadFrame(filePath, VM.CurrentFrameIndex, out int w, out int h);
                    canvas.SetPixels(pixels, w, h);
                }
                else
                {
                    var svc = new DicomService();
                    var pixels = svc.LoadDicomPixels(filePath, VM.CurrentFrameIndex, out int w, out int h);
                    canvas.SetPixels(pixels, w, h);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Canvas", $"Failed to render frame {VM.CurrentFrameIndex}", ex);
                VM.AddNotification(ViewModels.NotificationSeverity.Error,
                    $"Failed to render frame {VM.CurrentFrameIndex}",
                    ex.Message);
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
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (VM == null) return;

            // Don't intercept single-letter shortcuts while canvas is editing text
            bool canvasEditing = MainCanvas.Focusable && e.Source == MainCanvas;

            switch (e.Key)
            {
                case Key.Space: VM.TogglePlayCommand.Execute(null); break;
                case Key.Left: VM.PreviousFrameCommand.Execute(null); break;
                case Key.Right: VM.NextFrameCommand.Execute(null); break;
                case Key.Home: VM.FirstFrameCommand.Execute(null); break;
                case Key.End: VM.LastFrameCommand.Execute(null); break;
                case Key.Add:
                case Key.OemPlus: VM.ZoomInCommand.Execute(null); break;
                case Key.Subtract:
                case Key.OemMinus: VM.ZoomOutCommand.Execute(null); break;
                case Key.I: VM.ToggleInvertCommand.Execute(null); break;
                case Key.O:
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                        _ = VM.OpenFileCommand.ExecuteAsync(null);
                    break;
                case Key.L:
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                        VM.ToggleLogViewerCommand.Execute(null);
                    break;
                case Key.Z:
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    { MainCanvas.UndoAnnotation(); e.Handled = true; }
                    else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    { MainCanvas.RedoAnnotation(); e.Handled = true; }
                    break;
                case Key.Y:
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    { MainCanvas.RedoAnnotation(); e.Handled = true; }
                    break;
                case Key.F11:
                    ToggleFullscreen();
                    break;
                // Annotation tool shortcuts (only when not editing text on canvas)
                case Key.A:
                    if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
                        VM.SelectToolCommand.Execute("Arrow");
                    break;
                case Key.T:
                    VM.SelectToolCommand.Execute("TextLabel");
                    break;
                case Key.D:
                    VM.SelectToolCommand.Execute("Freehand");
                    break;
                case Key.C:
                    if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
                        VM.CycleAnnotationColorCommand.Execute(null);
                    break;
                case Key.F:
                    VM.FitToWindowCommand.Execute(null);
                    break;
                case Key.R:
                    VM.ResetViewCommand.Execute(null);
                    break;
                case Key.Escape:
                    VM.SelectToolCommand.Execute("None");
                    break;
            }
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
            vm.RequestBrowseDirectory = async () =>
            {
                var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Default Directory",
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
                VM.ActiveFile = fileVM;
            }
        }

        private void OnFileListPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border b && b.DataContext is DicomFileViewModel fileVM && VM != null)
            {
                VM.ActiveFile = fileVM;
            }
        }

        private void OnThumbnailPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border b && b.DataContext is ThumbnailViewModel thumbVM && VM != null)
            {
                VM.CurrentFrameIndex = thumbVM.FrameIndex;
            }
        }

        private void OnRightPanelHandlePressed(object? sender, PointerPressedEventArgs e)
        {
            if (VM != null) VM.IsRightPanelVisible = !VM.IsRightPanelVisible;
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
                    node.IsExpanded = !node.IsExpanded;
                }
                else
                {
                    _ = VM.OpenFilesFromPaths(new[] { node.FullPath });
                }
            }
        }
    } // End of MainWindow class
} // End of namespace