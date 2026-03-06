using Avalonia.Controls;
using Avalonia.Input;
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
                VM.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(MainWindowViewModel.ActiveFile) ||
                        args.PropertyName == nameof(MainWindowViewModel.CurrentFrameIndex))
                        UpdateCanvasImage();
                    if (args.PropertyName == nameof(MainWindowViewModel.CurrentFrameIndex))
                        ScrollFilmstripToCurrentFrame();
                };
        };
    }

    private MainWindowViewModel? VM => DataContext as MainWindowViewModel;
    private const double ThumbItemWidth = 80.0;

    private void ScrollFilmstripToCurrentFrame()
    {
        var scroller = this.FindControl<ScrollViewer>("FilmstripScroller");
        if (scroller == null || VM == null) return;
        double target = VM.CurrentFrameIndex * ThumbItemWidth - scroller.Viewport.Width / 2 + ThumbItemWidth / 2;
        double max = Math.Max(0, VM.TotalFrames * ThumbItemWidth - scroller.Viewport.Width);
        scroller.Offset = new Avalonia.Vector(Math.Max(0, Math.Min(target, max)), 0);
    }

    private void UpdateCanvasImage()
    {
        if (VM?.ActiveFile == null) return;
        var canvas = this.FindControl<DicomCanvas>("MainCanvas");
        if (canvas == null) return;
        var pixels = new DicomService().LoadDicomPixels(VM.ActiveFile.FilePath, VM.CurrentFrameIndex, out int w, out int h);
        canvas.SetPixels(pixels, w, h);
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (VM != null) VM.RequestOpenFile = OpenFileDialog;
    }

    private async Task OpenFileDialog()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Open DICOM", AllowMultiple = true });
        if (files.Any() && VM != null)
            await VM.OpenFilesFromPaths(files.Select(f => f.TryGetLocalPath() ?? "").Where(p => !string.IsNullOrEmpty(p)).ToArray());
    }

    private void OnRightPanelHandlePressed(object? sender, PointerPressedEventArgs e) => VM?.ToggleRightPanelCommand.Execute(null);
    private void OnFilmstripHandlePressed(object? sender, PointerPressedEventArgs e) => VM?.ToggleMiniFramesCommand.Execute(null);
    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e) { if (sender is Border b && b.DataContext is DicomFileViewModel f && VM != null) _ = VM.SelectFileCommand.ExecuteAsync(f); }
    private void OnFileListPointerPressed(object? sender, PointerPressedEventArgs e) { if (sender is Border b && b.DataContext is DicomFileViewModel f && VM != null) _ = VM.SelectFileCommand.ExecuteAsync(f); }
    private void OnThumbnailPointerPressed(object? sender, PointerPressedEventArgs e) { if (sender is Border b && b.DataContext is ThumbnailViewModel t && VM != null) VM.CurrentFrameIndex = t.FrameIndex; }
    private void OnDragOver(object? sender, DragEventArgs e) => e.DragEffects = DragDropEffects.Copy;
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (VM == null) return;
        var paths = (e.DataTransfer as Avalonia.Input.IDataObject)?.GetFiles()?.Select(f => f.TryGetLocalPath() ?? "").Where(p => !string.IsNullOrEmpty(p)).ToArray();
        if (paths?.Length > 0) await VM.OpenFilesFromPaths(paths);
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
            case Key.Add: case Key.OemPlus: VM.ZoomInCommand.Execute(null); break;
            case Key.Subtract: case Key.OemMinus: VM.ZoomOutCommand.Execute(null); break;
            case Key.F: VM.FitToWindowCommand.Execute(null); break;
            case Key.R: VM.ResetViewCommand.Execute(null); break;
        }
    }
}