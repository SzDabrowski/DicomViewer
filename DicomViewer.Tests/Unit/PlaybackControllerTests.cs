using DicomViewer.ViewModels;
using Xunit;

namespace DicomViewer.Tests.Unit;

public class PlaybackControllerTests
{
    private PlaybackController CreateController()
    {
        return new PlaybackController();
    }

    [Fact]
    public void ResetForNewFile_SetsTotalFrames_And_ResetsCurrentFrameIndex()
    {
        var controller = CreateController();
        controller.ResetForNewFile(10);

        Assert.Equal(10, controller.TotalFrames);
        Assert.Equal(0, controller.CurrentFrameIndex);
    }

    [Fact]
    public void ResetForNewFile_ResetsFrameIndex_WhenPreviouslyAdvanced()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);
        controller.NextFrame();
        controller.NextFrame();

        controller.ResetForNewFile(8);

        Assert.Equal(0, controller.CurrentFrameIndex);
        Assert.Equal(8, controller.TotalFrames);
    }

    [Fact]
    public void NextFrame_IncrementsCurrentFrameIndex()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);

        controller.NextFrame();

        Assert.Equal(1, controller.CurrentFrameIndex);
    }

    [Fact]
    public void NextFrame_WrapsAround_WhenLoopEnabled()
    {
        var controller = CreateController();
        controller.ResetForNewFile(3);

        controller.NextFrame(); // 1
        controller.NextFrame(); // 2
        controller.NextFrame(); // wraps to 0

        Assert.Equal(0, controller.CurrentFrameIndex);
    }

    [Fact]
    public void NextFrame_StaysAtLastFrame_WhenLoopDisabled()
    {
        var controller = CreateController();
        controller.LoopPlayback = false;
        controller.ResetForNewFile(3);

        controller.NextFrame(); // 1
        controller.NextFrame(); // 2
        controller.NextFrame(); // stays at 2

        Assert.Equal(2, controller.CurrentFrameIndex);
    }

    [Fact]
    public void PreviousFrame_DecrementsCurrentFrameIndex()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);
        controller.NextFrame();
        controller.NextFrame();

        controller.PreviousFrame();

        Assert.Equal(1, controller.CurrentFrameIndex);
    }

    [Fact]
    public void PreviousFrame_WrapsToLastFrame_FromZero_WhenLoopEnabled()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);

        controller.PreviousFrame();

        Assert.Equal(4, controller.CurrentFrameIndex);
    }

    [Fact]
    public void PreviousFrame_StaysAtZero_WhenLoopDisabled()
    {
        var controller = CreateController();
        controller.LoopPlayback = false;
        controller.ResetForNewFile(5);

        controller.PreviousFrame();

        Assert.Equal(0, controller.CurrentFrameIndex);
    }

    [Fact]
    public void StartPlayback_SetsIsPlayingTrue_WhenMultipleFrames()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);

        controller.StartPlayback();

        Assert.True(controller.IsPlaying);

        // Clean up to stop background task
        controller.StopPlayback();
    }

    [Fact]
    public void StartPlayback_DoesNotSetIsPlaying_WhenSingleFrame()
    {
        var controller = CreateController();
        controller.ResetForNewFile(1);

        controller.StartPlayback();

        Assert.False(controller.IsPlaying);
    }

    [Fact]
    public void StopPlayback_SetsIsPlayingFalse()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);
        controller.StartPlayback();

        controller.StopPlayback();

        Assert.False(controller.IsPlaying);
    }

    [Fact]
    public void StopPlayback_IsIdempotent_WhenNotPlaying()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);

        controller.StopPlayback();

        Assert.False(controller.IsPlaying);
    }

    [Theory]
    [InlineData(1)]
    public void SingleFrame_NextFrame_StaysAtZero_WhenLoopEnabled(int totalFrames)
    {
        var controller = CreateController();
        controller.ResetForNewFile(totalFrames);

        controller.NextFrame();

        Assert.Equal(0, controller.CurrentFrameIndex);
    }

    [Theory]
    [InlineData(1)]
    public void SingleFrame_PreviousFrame_StaysAtZero_WhenLoopEnabled(int totalFrames)
    {
        var controller = CreateController();
        controller.ResetForNewFile(totalFrames);

        controller.PreviousFrame();

        Assert.Equal(0, controller.CurrentFrameIndex);
    }

    [Fact]
    public void SingleFrame_StartPlayback_DoesNotPlay()
    {
        var controller = CreateController();
        controller.ResetForNewFile(1);

        controller.StartPlayback();

        Assert.False(controller.IsPlaying);
    }

    [Fact]
    public void CurrentFrameDisplay_IsOneBased()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);

        Assert.Equal(1, controller.CurrentFrameDisplay);

        controller.NextFrame();
        Assert.Equal(2, controller.CurrentFrameDisplay);
    }

    [Fact]
    public void FirstFrame_SetsIndexToZero()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);
        controller.NextFrame();
        controller.NextFrame();

        controller.FirstFrame();

        Assert.Equal(0, controller.CurrentFrameIndex);
    }

    [Fact]
    public void LastFrame_SetsIndexToTotalMinusOne()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);

        controller.LastFrame();

        Assert.Equal(4, controller.CurrentFrameIndex);
    }

    [Fact]
    public void TogglePlay_StartsPlayback_WhenNotPlaying()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);

        controller.TogglePlay();

        Assert.True(controller.IsPlaying);

        controller.StopPlayback();
    }

    [Fact]
    public void TogglePlay_StopsPlayback_WhenPlaying()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);
        controller.StartPlayback();

        controller.TogglePlay();

        Assert.False(controller.IsPlaying);
    }

    [Fact]
    public void ResetForNewFile_StopsPlayback()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);
        controller.StartPlayback();
        Assert.True(controller.IsPlaying);

        controller.ResetForNewFile(3);

        Assert.False(controller.IsPlaying);
    }

    [Fact]
    public void StatusChanged_FiresOnStartPlayback()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);
        string? statusMessage = null;
        controller.StatusChanged += msg => statusMessage = msg;

        controller.StartPlayback();

        Assert.NotNull(statusMessage);

        controller.StopPlayback();
    }

    [Fact]
    public void StatusChanged_FiresOnStopPlayback()
    {
        var controller = CreateController();
        controller.ResetForNewFile(5);
        controller.StartPlayback();

        string? statusMessage = null;
        controller.StatusChanged += msg => statusMessage = msg;

        controller.StopPlayback();

        Assert.NotNull(statusMessage);
    }
}
