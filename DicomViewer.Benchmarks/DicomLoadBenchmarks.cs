using BenchmarkDotNet.Attributes;
using DicomViewer.Services;
using FellowOakDicom;

namespace DicomViewer.Benchmarks;

/// <summary>
/// Benchmarks DICOM file operations: metadata parsing, pixel loading,
/// and series grouping. Requires a sample DICOM file.
///
/// Set the DICOM_BENCH_FILE environment variable to point to a test .dcm file,
/// or place a file named "sample.dcm" next to the benchmark exe.
///
/// Set DICOM_BENCH_DIR to a directory of DICOM files for the grouping benchmark.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class DicomLoadBenchmarks
{
    private DicomService _dicomService = null!;
    private string _sampleFile = null!;
    private string[]? _directoryFiles;
    private bool _hasSample;

    [GlobalSetup]
    public void Setup()
    {
        // Initialize fo-dicom codecs once
        new DicomSetupBuilder()
            .RegisterServices(s => s
                .AddFellowOakDicom()
                .AddTranscoderManager<FellowOakDicom.Imaging.NativeCodec.NativeTranscoderManager>())
            .SkipValidation()
            .Build();

        _dicomService = new DicomService();

        _sampleFile = Environment.GetEnvironmentVariable("DICOM_BENCH_FILE")
            ?? Path.Combine(AppContext.BaseDirectory, "sample.dcm");

        _hasSample = File.Exists(_sampleFile);
        if (!_hasSample)
        {
            Console.WriteLine($"[WARN] No DICOM file found at '{_sampleFile}'. " +
                "DICOM benchmarks will be skipped. Set DICOM_BENCH_FILE env var.");
        }

        var benchDir = Environment.GetEnvironmentVariable("DICOM_BENCH_DIR");
        if (!string.IsNullOrEmpty(benchDir) && Directory.Exists(benchDir))
        {
            _directoryFiles = Directory.GetFiles(benchDir, "*.dcm", SearchOption.AllDirectories);
            Console.WriteLine($"[INFO] Found {_directoryFiles.Length} DICOM files in bench directory");
        }
    }

    [Benchmark]
    public object? GetMetadata()
    {
        if (!_hasSample) return null;
        return _dicomService.GetMetadata(_sampleFile);
    }

    [Benchmark]
    public object? LoadPixels_Frame0()
    {
        if (!_hasSample) return null;
        // Clear cache to force a full decode
        _dicomService.ClearCache();
        return _dicomService.LoadDicomPixels(_sampleFile, 0, out _, out _, out _);
    }

    [Benchmark]
    public object? LoadPixels_Cached()
    {
        if (!_hasSample) return null;
        // First call populates cache, second call should be near-instant
        _dicomService.LoadDicomPixels(_sampleFile, 0, out _, out _, out _);
        return _dicomService.LoadDicomPixels(_sampleFile, 0, out _, out _, out _);
    }

    [Benchmark]
    public object? GroupFilesIntoStacks()
    {
        if (_directoryFiles == null || _directoryFiles.Length == 0) return null;
        return _dicomService.GroupFilesIntoStacks(_directoryFiles);
    }
}
