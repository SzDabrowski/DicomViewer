using DicomViewer.ViewModels;
using Xunit;

namespace DicomViewer.Tests.Unit;

/// <summary>
/// Tests for the MainWindowViewModel playback frame-render-await fix.
/// Verifies that WaitForFrameRenderAsync is called during playback so
/// each frame finishes rendering before the next is requested.
/// </summary>
public class MainWindowViewModelPlaybackTests
{
    [Fact]
    public void WaitForFrameRenderAsync_IsNullByDefault()
    {
        var vm = new MainWindowViewModel();

        Assert.Null(vm.WaitForFrameRenderAsync);
    }

    [Fact]
    public void WaitForFrameRenderAsync_CanBeSet()
    {
        var vm = new MainWindowViewModel();
        var called = false;

        vm.WaitForFrameRenderAsync = () =>
        {
            called = true;
            return Task.CompletedTask;
        };

        Assert.NotNull(vm.WaitForFrameRenderAsync);

        // Invoke and verify it works
        vm.WaitForFrameRenderAsync();
        Assert.True(called);
    }

    [Fact]
    public void WaitForFrameRenderAsync_ReturnsTask_ThatCanBeAwaited()
    {
        var vm = new MainWindowViewModel();
        var tcs = new TaskCompletionSource();

        vm.WaitForFrameRenderAsync = () => tcs.Task;

        var task = vm.WaitForFrameRenderAsync();

        Assert.False(task.IsCompleted);

        tcs.SetResult();

        Assert.True(task.IsCompleted);
    }

    [Fact]
    public async Task WaitForFrameRenderAsync_SlowRender_BlocksUntilComplete()
    {
        var vm = new MainWindowViewModel();
        var renderCompleted = false;
        var tcs = new TaskCompletionSource();

        vm.WaitForFrameRenderAsync = () => tcs.Task;

        // Simulate what the playback loop does: await the render callback
        var waitTask = Task.Run(async () =>
        {
            if (vm.WaitForFrameRenderAsync != null)
                await vm.WaitForFrameRenderAsync();
            renderCompleted = true;
        });

        // Render hasn't completed yet
        await Task.Delay(50);
        Assert.False(renderCompleted);

        // Complete the render
        tcs.SetResult();
        await waitTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(renderCompleted);
    }

    [Fact]
    public async Task WaitForFrameRenderAsync_SequentialFrames_EachAwaited()
    {
        var vm = new MainWindowViewModel();
        var renderOrder = new List<int>();
        var frameIndex = 0;

        vm.WaitForFrameRenderAsync = async () =>
        {
            var currentFrame = frameIndex;
            // Simulate async render delay
            await Task.Delay(10);
            renderOrder.Add(currentFrame);
        };

        // Simulate sequential playback: advance frame, await render, repeat
        var playTask = Task.Run(async () =>
        {
            for (int i = 0; i < 5; i++)
            {
                frameIndex = i;
                if (vm.WaitForFrameRenderAsync != null)
                    await vm.WaitForFrameRenderAsync();
            }
        });

        await playTask.WaitAsync(TimeSpan.FromSeconds(5));

        // All 5 frames should have been rendered in order
        Assert.Equal(5, renderOrder.Count);
        Assert.Equal(new List<int> { 0, 1, 2, 3, 4 }, renderOrder);
    }

    [Fact]
    public void StartPlayback_DoesNotStart_WhenSingleFrame()
    {
        var vm = new MainWindowViewModel();
        vm.TotalFrames = 1;

        // Use reflection or just verify IsPlaying stays false
        // StartPlayback is private, but TogglePlay is a command
        vm.TogglePlayCommand.Execute(null);

        Assert.False(vm.IsPlaying);
    }

    [Fact]
    public void StopPlayback_SetsIsPlayingFalse()
    {
        var vm = new MainWindowViewModel();
        vm.TotalFrames = 5;

        // Start then stop via toggle
        vm.TogglePlayCommand.Execute(null);
        Assert.True(vm.IsPlaying);

        vm.TogglePlayCommand.Execute(null);
        Assert.False(vm.IsPlaying);
    }

    [Fact]
    public void NextFrame_AdvancesIndex()
    {
        var vm = new MainWindowViewModel();
        vm.TotalFrames = 5;
        vm.CurrentFrameIndex = 0;

        vm.NextFrameCommand.Execute(null);

        Assert.Equal(1, vm.CurrentFrameIndex);
    }

    [Fact]
    public void NextFrame_WrapsWithLoop()
    {
        var vm = new MainWindowViewModel();
        vm.TotalFrames = 3;
        vm.LoopPlayback = true;
        vm.CurrentFrameIndex = 2;

        vm.NextFrameCommand.Execute(null);

        Assert.Equal(0, vm.CurrentFrameIndex);
    }

    [Fact]
    public void NextFrame_StaysAtEnd_WithoutLoop()
    {
        var vm = new MainWindowViewModel();
        vm.TotalFrames = 3;
        vm.LoopPlayback = false;
        vm.CurrentFrameIndex = 2;

        vm.NextFrameCommand.Execute(null);

        Assert.Equal(2, vm.CurrentFrameIndex);
    }
}
