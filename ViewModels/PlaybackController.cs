using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DicomViewer.Constants;
using DicomViewer.Services;
using Avalonia.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DicomViewer.ViewModels;

/// <summary>
/// Manages frame playback state: play/pause, FPS, looping, frame navigation.
/// Extracted from MainWindowViewModel to follow Single Responsibility Principle.
/// </summary>
public partial class PlaybackController : ViewModelBase
{
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private CancellationTokenSource? _playCts;

    [ObservableProperty] private int _currentFrameIndex;
    [ObservableProperty] private int _totalFrames = 1;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private int _playbackFps = UIConstants.DefaultPlaybackFps;
    [ObservableProperty] private bool _loopPlayback = true;

    /// <summary>1-based frame number for display.</summary>
    public int CurrentFrameDisplay => CurrentFrameIndex + 1;

    /// <summary>Raised when the status message should be updated.</summary>
    public event Action<string>? StatusChanged;

    partial void OnCurrentFrameIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentFrameDisplay));
    }

    [RelayCommand]
    public void TogglePlay()
    {
        if (IsPlaying) StopPlayback();
        else StartPlayback();
    }

    [RelayCommand]
    public void PreviousFrame()
    {
        CurrentFrameIndex = CurrentFrameIndex > 0
            ? CurrentFrameIndex - 1
            : (LoopPlayback ? TotalFrames - 1 : 0);
    }

    [RelayCommand]
    public void NextFrame()
    {
        CurrentFrameIndex = CurrentFrameIndex < TotalFrames - 1
            ? CurrentFrameIndex + 1
            : (LoopPlayback ? 0 : TotalFrames - 1);
    }

    [RelayCommand]
    public void FirstFrame() => CurrentFrameIndex = 0;

    [RelayCommand]
    public void LastFrame() => CurrentFrameIndex = TotalFrames - 1;

    public void StartPlayback()
    {
        if (TotalFrames <= 1) return;
        IsPlaying = true;
        _playCts = new CancellationTokenSource();
        var token = _playCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1000 / Math.Max(1, PlaybackFps), token);
                    if (token.IsCancellationRequested) break;
                    Dispatcher.UIThread.Post(NextFrame);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when StopPlayback cancels the token
            }
        }, token);
        StatusChanged?.Invoke($"{_loc["Playing"]} - {PlaybackFps} {_loc["FPS"]}");
    }

    public void StopPlayback()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        _playCts?.Cancel();
        _playCts?.Dispose();
        _playCts = null;
        StatusChanged?.Invoke(_loc["Paused"]);
    }

    /// <summary>
    /// Resets frame state for a new file. Uses direct field write to avoid
    /// triggering canvas update before ActiveFile is set.
    /// </summary>
    public void ResetForNewFile(int totalFrames)
    {
        StopPlayback();
        _currentFrameIndex = 0;
        TotalFrames = totalFrames;
        OnPropertyChanged(nameof(CurrentFrameIndex));
        OnPropertyChanged(nameof(CurrentFrameDisplay));
    }
}
