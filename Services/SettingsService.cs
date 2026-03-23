using System;
using System.Collections.Generic;
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

public class KeyBinding
{
    public string Key { get; set; } = string.Empty;
    public bool Ctrl { get; set; }
    public bool Shift { get; set; }
    public bool Alt { get; set; }

    public string DisplayString
    {
        get
        {
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            parts.Add(FriendlyName(Key));
            return string.Join(" + ", parts);
        }
    }

    private static string FriendlyName(string key) => key switch
    {
        "OemPlus" => "+",
        "OemMinus" => "-",
        "OemPeriod" => ".",
        "OemComma" => ",",
        "OemTilde" => "~",
        "Oem1" => ";",
        "Oem2" => "/",
        "Oem3" => "`",
        "Oem4" => "[",
        "Oem5" => "\\",
        "Oem6" => "]",
        "Oem7" => "'",
        "Back" => "Backspace",
        "Return" => "Enter",
        "Capital" => "CapsLock",
        "Prior" => "PageUp",
        "Next" => "PageDown",
        var k when k.Length == 2 && k[0] == 'D' && char.IsDigit(k[1]) => k[1].ToString(),
        _ => key
    };
}

public class KeyBindingSettings
{
    // Playback
    public KeyBinding TogglePlay { get; set; } = new() { Key = "Space" };
    public KeyBinding PreviousFrame { get; set; } = new() { Key = "Left" };
    public KeyBinding NextFrame { get; set; } = new() { Key = "Right" };
    public KeyBinding FirstFrame { get; set; } = new() { Key = "Home" };
    public KeyBinding LastFrame { get; set; } = new() { Key = "End" };

    // View
    public KeyBinding ZoomIn { get; set; } = new() { Key = "OemPlus" };
    public KeyBinding ZoomOut { get; set; } = new() { Key = "OemMinus" };
    public KeyBinding FitToWindow { get; set; } = new() { Key = "F" };
    public KeyBinding ResetView { get; set; } = new() { Key = "R" };
    public KeyBinding ToggleInvert { get; set; } = new() { Key = "I" };
    public KeyBinding ToggleFullscreen { get; set; } = new() { Key = "F11" };

    // Tools
    public KeyBinding ToolArrow { get; set; } = new() { Key = "A" };
    public KeyBinding ToolText { get; set; } = new() { Key = "T" };
    public KeyBinding ToolFreehand { get; set; } = new() { Key = "D" };
    public KeyBinding CycleColor { get; set; } = new() { Key = "C" };
    public KeyBinding DeselectTool { get; set; } = new() { Key = "Escape" };

    // File
    public KeyBinding OpenFile { get; set; } = new() { Key = "O", Ctrl = true };
    public KeyBinding OpenLogs { get; set; } = new() { Key = "L", Ctrl = true };

    // Edit
    public KeyBinding Undo { get; set; } = new() { Key = "Z", Ctrl = true };
    public KeyBinding Redo { get; set; } = new() { Key = "Y", Ctrl = true };
}

public class AppSettings
{
    public string DefaultDirectory { get; set; } = string.Empty;
    public bool ShowTooltips { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public StartupWindowMode StartupWindowMode { get; set; } = StartupWindowMode.Windowed;
    public string Language { get; set; } = "en";
    public KeyBindingSettings KeyBindings { get; set; } = new();
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
