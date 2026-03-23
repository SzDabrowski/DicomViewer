using BenchmarkDotNet.Attributes;
using DicomViewer.Controls;

namespace DicomViewer.Benchmarks;

/// <summary>
/// Benchmarks the RGBA buffer building — the hot path that runs on every
/// frame render and during playback (on background thread).
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class RgbaBufferBenchmarks
{
    private ushort[] _grayscalePixels = null!;
    private ushort[] _colorPixels = null!;

    [Params(256, 512, 1024)]
    public int ImageSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        int count = ImageSize * ImageSize;
        var rng = new Random(42);

        // Grayscale: single plane, values 0-65535
        _grayscalePixels = new ushort[count];
        for (int i = 0; i < count; i++)
            _grayscalePixels[i] = (ushort)rng.Next(0, 65536);

        // Color: 3 planes (R, G, B) packed sequentially
        _colorPixels = new ushort[count * 3];
        for (int i = 0; i < count * 3; i++)
            _colorPixels[i] = (ushort)rng.Next(0, 65536);
    }

    [Benchmark(Baseline = true)]
    public byte[] Grayscale_Normal()
    {
        return DicomCanvas.BuildRgbaBuffer(
            _grayscalePixels, ImageSize, ImageSize,
            isColor: false, windowCenter: 32000, windowWidth: 65000, isInverted: false);
    }

    [Benchmark]
    public byte[] Grayscale_Inverted()
    {
        return DicomCanvas.BuildRgbaBuffer(
            _grayscalePixels, ImageSize, ImageSize,
            isColor: false, windowCenter: 32000, windowWidth: 65000, isInverted: true);
    }

    [Benchmark]
    public byte[] Grayscale_NarrowWindow()
    {
        // Narrow window (e.g. Brain preset) — more pixels clamp to 0/255
        return DicomCanvas.BuildRgbaBuffer(
            _grayscalePixels, ImageSize, ImageSize,
            isColor: false, windowCenter: 32000, windowWidth: 500, isInverted: false);
    }

    [Benchmark]
    public byte[] Color_Normal()
    {
        return DicomCanvas.BuildRgbaBuffer(
            _colorPixels, ImageSize, ImageSize,
            isColor: true, windowCenter: 32000, windowWidth: 65000, isInverted: false);
    }

    [Benchmark]
    public byte[] Color_Inverted()
    {
        return DicomCanvas.BuildRgbaBuffer(
            _colorPixels, ImageSize, ImageSize,
            isColor: true, windowCenter: 32000, windowWidth: 65000, isInverted: true);
    }
}
