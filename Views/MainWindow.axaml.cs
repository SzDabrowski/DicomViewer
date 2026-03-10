using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DicomViewer.ViewModels;
using DicomViewer.Controls;
using DicomViewer.Services;
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

            var maxIcon = this.FindControl<TextBlock>("MaximiseIcon")!;
            this.FindControl<Button>("BtnMaximise")!.Click += (_, _) =>
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                maxIcon.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
            };

            this.FindControl<Button>("BtnClose")!.Click += (_, _)
                => Close();

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
                };

                VM.LoadSettings();
            };
        }

        private MainWindowViewModel? VM => DataContext as MainWindowViewModel;

        private void UpdateCanvasImage()
        {
            if (VM?.ActiveFile == null) { MainCanvas.ClearAllData(); return; }
            var filePath = VM.ActiveFile.FilePath;

            if (ImageService.IsSupported(filePath))
            {
                MainCanvas.Metadata = null;
                MainCanvas.CurrentFrameIndex = 0;
                var imgSvc = new ImageService();
                var pixels = imgSvc.LoadPixels(filePath, out int w, out int h);
                MainCanvas.SetPixels(pixels, w, h);
            }
            else if (VideoService.IsSupported(filePath))
            {
                MainCanvas.Metadata = null;
                MainCanvas.CurrentFrameIndex = VM.CurrentFrameIndex;
                var vidSvc = new VideoService();
                var pixels = vidSvc.LoadFrame(filePath, VM.CurrentFrameIndex, out int w, out int h);
                MainCanvas.SetPixels(pixels, w, h);
            }
            else
            {
                var svc = new DicomService();

                if (MainCanvas.Metadata == null || MainCanvas.Metadata.PatientId != VM.ActiveFile.PatientId)
                {
                    var metadata = svc.GetMetadata(filePath);
                    if (MainCanvas.Metadata != null && MainCanvas.Metadata.PatientId != metadata.PatientId)
                        MainCanvas.ClearAllData();

                    MainCanvas.Metadata = metadata;
                }

                MainCanvas.CurrentFrameIndex = VM.CurrentFrameIndex;
                var pixels = svc.LoadDicomPixels(filePath, VM.CurrentFrameIndex, out int w, out int h);
                MainCanvas.SetPixels(pixels, w, h);
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
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (VM == null) return;
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
                case Key.F: VM.FitToWindowCommand.Execute(null); break;
                case Key.R: VM.ResetViewCommand.Execute(null); break;
                case Key.I: VM.ToggleInvertCommand.Execute(null); break;
                case Key.O:
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                        _ = VM.OpenFileCommand.ExecuteAsync(null);
                    break;
            }
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