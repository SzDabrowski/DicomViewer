using BenchmarkDotNet.Attributes;
using DicomViewer.Services;

namespace DicomViewer.Benchmarks;

/// <summary>
/// Benchmarks startup-path operations: settings load/save,
/// localization initialization, and logging throughput.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class StartupBenchmarks
{
    private SettingsService _settingsService = null!;

    [GlobalSetup]
    public void Setup()
    {
        _settingsService = new SettingsService();
    }

    [Benchmark]
    public object LoadSettings()
    {
        return _settingsService.Load();
    }

    [Benchmark]
    public void SaveSettings()
    {
        var settings = _settingsService.Load();
        _settingsService.Save(settings);
    }

    [Benchmark]
    public void SetLanguage_English()
    {
        LocalizationService.Instance.SetLanguage("en");
    }

    [Benchmark]
    public void LoggingThroughput_100()
    {
        var log = LoggingService.Instance;
        for (int i = 0; i < 100; i++)
            log.Debug("Bench", $"Test message {i}");
    }
}
