using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DicomViewer.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DicomViewer.ViewModels;

public partial class LogViewerViewModel : ViewModelBase
{
    [ObservableProperty] private LogLevel _selectedFilter = LogLevel.Debug;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _autoScroll = true;

    public ObservableCollection<LogEntry> FilteredEntries { get; } = new();

    private readonly LoggingService _log = LoggingService.Instance;

    public LogViewerViewModel()
    {
        _log.LogAdded += OnLogAdded;
        Refresh();
    }

    partial void OnSelectedFilterChanged(LogLevel value) => Refresh();
    partial void OnSearchTextChanged(string value) => Refresh();

    private void OnLogAdded(LogEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (MatchesFilter(entry))
            {
                FilteredEntries.Add(entry);

                // Keep max 500 visible entries
                while (FilteredEntries.Count > 500)
                    FilteredEntries.RemoveAt(0);
            }
        });
    }

    private bool MatchesFilter(LogEntry entry)
    {
        if (entry.Level < SelectedFilter) return false;
        if (!string.IsNullOrEmpty(SearchText) &&
            !entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
            !entry.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    [RelayCommand]
    private void Refresh()
    {
        FilteredEntries.Clear();
        foreach (var entry in _log.GetRecentEntries().Where(MatchesFilter))
            FilteredEntries.Add(entry);
    }

    [RelayCommand]
    private void Clear()
    {
        FilteredEntries.Clear();
    }

    [RelayCommand]
    private void SetFilter(string level)
    {
        if (Enum.TryParse<LogLevel>(level, true, out var parsed))
            SelectedFilter = parsed;
    }
}
