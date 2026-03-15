using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DicomViewer.Services;

public enum LogLevel { Debug, Info, Warning, Error }

public record LogEntry(DateTime Timestamp, LogLevel Level, string Category, string Message, string? Details = null)
{
    public string FormattedTime => Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    public string LevelLabel => Level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Info => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        _ => "???"
    };
}

public sealed class LoggingService
{
    public static LoggingService Instance { get; } = new();

    private readonly object _lock = new();
    private readonly LinkedList<LogEntry> _buffer = new();
    private const int MaxBufferSize = 500;
    private readonly string _logDir;
    private string? _currentLogFile;
    private string? _currentLogDate;

    public event Action<LogEntry>? LogAdded;

    private LoggingService()
    {
        _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DicomViewer", "logs");
    }

    public void Log(LogLevel level, string category, string message, string? details = null)
    {
        var entry = new LogEntry(DateTime.Now, level, category, message, details);

        lock (_lock)
        {
            _buffer.AddLast(entry);
            while (_buffer.Count > MaxBufferSize)
                _buffer.RemoveFirst();
        }

        WriteToFile(entry);
        LogAdded?.Invoke(entry);
    }

    public void Debug(string category, string message) => Log(LogLevel.Debug, category, message);
    public void Info(string category, string message) => Log(LogLevel.Info, category, message);
    public void Warning(string category, string message, string? details = null) => Log(LogLevel.Warning, category, message, details);
    public void Error(string category, string message, string? details = null) => Log(LogLevel.Error, category, message, details);
    public void Error(string category, string message, Exception ex) => Log(LogLevel.Error, category, message, $"{ex.Message}\n{ex.StackTrace}");

    public IReadOnlyList<LogEntry> GetRecentEntries()
    {
        lock (_lock)
        {
            return _buffer.ToList();
        }
    }

    public string GetLogFilePath()
    {
        EnsureLogFile();
        return _currentLogFile ?? _logDir;
    }

    private void WriteToFile(LogEntry entry)
    {
        try
        {
            EnsureLogFile();
            if (_currentLogFile == null) return;

            var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{entry.LevelLabel}] [{entry.Category}] {entry.Message}";
            if (!string.IsNullOrEmpty(entry.Details))
                line += $"\n  {entry.Details.Replace("\n", "\n  ")}";

            lock (_lock)
            {
                File.AppendAllText(_currentLogFile, line + Environment.NewLine);
            }
        }
        catch
        {
            // Cannot log the failure to log — avoid infinite recursion
        }
    }

    private void EnsureLogFile()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (_currentLogDate == today && _currentLogFile != null) return;

        try
        {
            Directory.CreateDirectory(_logDir);
            _currentLogDate = today;
            _currentLogFile = Path.Combine(_logDir, $"dicomviewer-{today}.log");
            CleanOldLogs();
        }
        catch
        {
            _currentLogFile = null;
        }
    }

    private void CleanOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-7);
            foreach (var file in Directory.GetFiles(_logDir, "dicomviewer-*.log"))
            {
                if (File.GetCreationTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch { }
    }
}
