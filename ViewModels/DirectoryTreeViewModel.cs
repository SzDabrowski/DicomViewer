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
    public string Icon => IsDirectory ? (IsExpanded ? "\u25BC" : "\u25B6") : GetFileIcon();
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
            ".dcm" or ".dicom" => "\u2695",  // medical
            ".avi" or ".mp4" or ".mkv" or ".mov" or ".wmv" => "\u25B6",  // play
            _ => "\U0001F5BC"  // image
        };
    }
}
