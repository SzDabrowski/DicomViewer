using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DicomViewer.Services;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StartupWindowMode
{
    Windowed,
    Maximized,
    Fullscreen
}

public class AppSettings
{
    public string DefaultDirectory { get; set; } = string.Empty;
    public bool ShowTooltips { get; set; } = true;
    public StartupWindowMode StartupWindowMode { get; set; } = StartupWindowMode.Windowed;
}

public class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DicomViewer");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            LoggingService.Instance.Warning("Settings", "Failed to load settings, using defaults", ex.Message);
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            LoggingService.Instance.Error("Settings", "Failed to save settings", ex);
        }
    }
}
