using CommunityToolkit.Mvvm.ComponentModel;
using IconPacks.Avalonia.Codicons;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace DicomViewer.ViewModels;

public partial class FileTreeNodeViewModel : ViewModelBase
{
    private static readonly string[] SupportedExtensions =
    {
        ".dcm", ".dicom", ".jpg", ".jpeg", ".png", ".bmp",
        ".tiff", ".tif", ".gif", ".webp", ".avi", ".mp4", ".mkv", ".mov", ".wmv"
    };

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public PackIconCodiconsKind IconKind => IsDirectory ? (IsExpanded ? PackIconCodiconsKind.ChevronDown : PackIconCodiconsKind.ChevronRight) : GetFileIconKind();
    public PackIconCodiconsKind FolderIconKind => IsDirectory ? (IsExpanded ? PackIconCodiconsKind.FolderOpened : PackIconCodiconsKind.Folder) : PackIconCodiconsKind.File;
    public ObservableCollection<FileTreeNodeViewModel> Children { get; } = new();

    public FileTreeNodeViewModel(string path, bool isDirectory)
    {
        FullPath = path;
        Name = Path.GetFileName(path);
        IsDirectory = isDirectory;

        if (isDirectory)
            LoadChildren();
    }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(IconKind));
        OnPropertyChanged(nameof(FolderIconKind));
    }

    private void LoadChildren()
    {
        Children.Clear();
        try
        {
            // Add subdirectories
            foreach (var dir in Directory.GetDirectories(FullPath).OrderBy(d => Path.GetFileName(d)))
            {
                // Only add directories that contain supported files (direct or nested)
                if (HasSupportedFiles(dir))
                    Children.Add(new FileTreeNodeViewModel(dir, true));
            }

            // Add supported files
            foreach (var file in Directory.GetFiles(FullPath)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => Path.GetFileName(f)))
            {
                Children.Add(new FileTreeNodeViewModel(file, false));
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Services.LoggingService.Instance.Warning("FileTree", $"Access denied: {FullPath}", ex.Message);
        }
        catch (IOException ex)
        {
            Services.LoggingService.Instance.Warning("FileTree", $"IO error reading: {FullPath}", ex.Message);
        }
    }

    private static bool HasSupportedFiles(string dirPath)
    {
        try
        {
            if (Directory.GetFiles(dirPath)
                .Any(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
                return true;

            return Directory.GetDirectories(dirPath).Any(HasSupportedFiles);
        }
        catch (Exception ex)
        {
            Services.LoggingService.Instance.Debug("FileTree", $"Cannot scan {dirPath}: {ex.Message}");
            return false;
        }
    }

    private PackIconCodiconsKind GetFileIconKind()
    {
        var ext = Path.GetExtension(FullPath).ToLowerInvariant();
        return ext switch
        {
            ".dcm" or ".dicom" => PackIconCodiconsKind.Heart,
            ".avi" or ".mp4" or ".mkv" or ".mov" or ".wmv" => PackIconCodiconsKind.Play,
            ".jpg" or ".jpeg" or ".png" or ".bmp" or ".tiff" or ".tif" or ".gif" or ".webp" => PackIconCodiconsKind.FileMedia,
            _ => PackIconCodiconsKind.File
        };
    }
}
