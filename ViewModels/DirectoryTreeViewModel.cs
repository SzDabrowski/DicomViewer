using CommunityToolkit.Mvvm.ComponentModel;
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
    public string Icon => IsDirectory ? (IsExpanded ? "\uEAB4" : "\uEAB6") : GetFileIcon();
    public string? FolderIcon => IsDirectory ? (IsExpanded ? "\uEAF7" : "\uEA83") : null;
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
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(FolderIcon));
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
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
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
        catch { return false; }
    }

    private string GetFileIcon()
    {
        var ext = Path.GetExtension(FullPath).ToLowerInvariant();
        return ext switch
        {
            ".dcm" or ".dicom" => "\uEB05",   // heart (medical)
            ".avi" or ".mp4" or ".mkv" or ".mov" or ".wmv" => "\uEB2C",  // play (video)
            ".jpg" or ".jpeg" or ".png" or ".bmp" or ".tiff" or ".tif" or ".gif" or ".webp" => "\uEAEA",  // file-media (image)
            _ => "\uEA7B"  // file (generic)
        };
    }
}
