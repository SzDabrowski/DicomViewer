using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DicomViewer.ViewModels;
using DicomViewer.Controls;
using DicomViewer.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DicomViewer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        KeyDown += OnKeyDown;

        DataContextChanged += (s, e) =>
        {
            if (VM != null)
            {
                VM.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(MainWindowViewModel.ActiveFile) ||
                        args.PropertyName == nameof(MainWindowViewModel.CurrentFrameIndex))
                    {
                        UpdateCanvasImage();
                    }

                    if (args.PropertyName == nameof(MainWindowViewModel.CurrentFrameIndex))
                    {
                        ScrollFilmstripToCurrentFrame();
                    }
                };
            }
        };
    }

    private MainWindowViewModel? VM => DataContext as MainWindowViewModel;

    // Each thumbnail is 74px wide + 3px margin on each side = 80px per item
    private const double ThumbItemWidth = 80.0;

    private void ScrollFilmstripToCurrentFrame()
    {
        var scroller = this.FindControl<ScrollViewer>("FilmstripScroller");
        if (scroller == null || VM == null) return;

        // Center the current thumb in the visible viewport
        double targetOffset = VM.CurrentFrameIndex * ThumbItemWidth
                              - scroller.Viewport.Width / 2
                              + ThumbItemWidth / 2;

        // Clamp to valid scroll range
        double maxOffset = Math.Max(0, VM.TotalFrames * ThumbItemWidth - scroller.Viewport.Width);
        targetOffset = Math.Max(0, Math.Min(targetOffset, maxOffset));

        scroller.Offset = new Avalonia.Vector(targetOffset, 0);
    }

    private void UpdateCanvasImage()
    {
        if (VM?.ActiveFile == null) return;
        var canvas = this.FindControl<DicomCanvas>("MainCanvas");
        if (canvas == null) return;
        var svc = new DicomService();
        var pixels = svc.LoadDicomPixels(VM.ActiveFile.FilePath, VM.CurrentFrameIndex, out int w, out int h);
        canvas.SetPixels(pixels, w, h);
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (VM != null) VM.RequestOpenFile = OpenFileDialog;

        // Wire filmstrip scroll: vertical wheel → horizontal pan
        var filmstrip = this.FindControl<ScrollViewer>("FilmstripScroller");
        if (filmstrip != null)
        {
            filmstrip.AddHandler(PointerWheelChangedEvent, OnFilmstripWheel, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }

        // Wire canvas events → ViewModel so mouse interactions update VM state
        if (MainCanvas != null)
        {
            MainCanvas.ZoomLevelChanged += (_, z) => { if (VM != null) VM.ZoomLevel = z; };
            MainCanvas.PanChanged       += (_, p) => { if (VM != null) { VM.PanX = p.X; VM.PanY = p.Y; } };
            MainCanvas.WindowLevelChanged += (_, wl) =>
            {
                if (VM != null) { VM.WindowCenter = wl.Center; VM.WindowWidth = wl.Width; }
            };
            MainCanvas.FrameScrolled += (_, dir) =>
            {
                if (VM == null) return;
                if (dir > 0) VM.NextFrameCommand.Execute(null);
                else         VM.PreviousFrameCommand.Execute(null);
            };
        }
    }

    private async Task OpenFileDialog()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open DICOM",
            AllowMultiple = true
        });
        if (files.Any() && VM != null)
        {
            var paths = files.Select(f => f.TryGetLocalPath() ?? "")
                             .Where(p => !string.IsNullOrEmpty(p))
                             .ToArray();
            await VM.OpenFilesFromPaths(paths);
        }
    }

    // ── Right panel collapse strip click ───────────────────────────────────────────────────────
    private void OnRightPanelHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        VM?.ToggleRightPanelCommand.Execute(null);
    }

    // ── Filmstrip collapse handle click ─────────────────────────────────────────────────────────
    private void OnFilmstripHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        VM?.ToggleMiniFramesCommand.Execute(null);
    }

    // ── File tab click (Row 1 tab strip) ───────────────────────────────────────────────────────
    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is DicomFileViewModel file && VM != null)
            _ = VM.SelectFileCommand.ExecuteAsync(file);
    }

    // ── Right-panel file list click ──────────────────────────────────────────────────────────
    private void OnFileListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is DicomFileViewModel file && VM != null)
            _ = VM.SelectFileCommand.ExecuteAsync(file);
    }

    // ── Filmstrip vertical scroll → horizontal scroll ───────────────────────
    private void OnFilmstripWheel(object? sender, PointerWheelEventArgs e)
    {
        var scroller = this.FindControl<ScrollViewer>("FilmstripScroller");
        if (scroller == null) return;

        // Translate vertical delta to horizontal offset (3 thumbs per notch feels natural)
        double step = ThumbItemWidth * 3 * (e.Delta.Y > 0 ? -1 : 1);
        scroller.Offset = new Avalonia.Vector(
            Math.Clamp(scroller.Offset.X + step, 0, scroller.ScrollBarMaximum.X), 0);

        e.Handled = true; // prevent bubbling to the canvas
    }

    // ── Filmstrip thumbnail click → jump to that frame ───────────────────────
    private void OnThumbnailPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is ThumbnailViewModel thumb && VM != null)
            VM.CurrentFrameIndex = thumb.FrameIndex;
    }

    // ── Drag & drop ──────────────────────────────────────────────────────────────────────────
    private void OnDragOver(object? sender, DragEventArgs e) => e.DragEffects = DragDropEffects.Copy;

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (VM == null) return;
        var paths = (e.DataTransfer as Avalonia.Input.IDataObject)?.GetFiles()
                         ?.Select(f => f.TryGetLocalPath() ?? "")
                          .Where(p => !string.IsNullOrEmpty(p))
                          .ToArray();
        if (paths != null && paths.Length > 0)
            await VM.OpenFilesFromPaths(paths);
    }

    // ── Keyboard shortcuts ───────────────────────────────────────────────────────────────────────
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
        }
    }
}
