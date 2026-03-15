using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace DicomViewer.ViewModels;

public enum NotificationSeverity { Info, Warning, Error }

public partial class NotificationViewModel : ObservableObject
{
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private NotificationSeverity _severity = NotificationSeverity.Info;
    [ObservableProperty] private string _details = string.Empty;
    [ObservableProperty] private bool _isVisible = true;

    public DateTime Timestamp { get; } = DateTime.Now;

    /// <summary>Auto-dismiss delay in ms. 0 = persist until manually dismissed.</summary>
    public int AutoDismissMs => Severity switch
    {
        NotificationSeverity.Info => 5000,
        NotificationSeverity.Warning => 10000,
        NotificationSeverity.Error => 0,
        _ => 5000,
    };

    public string SeverityLabel => Severity switch
    {
        NotificationSeverity.Info => "INFO",
        NotificationSeverity.Warning => "WARNING",
        NotificationSeverity.Error => "ERROR",
        _ => "INFO",
    };

    public static NotificationViewModel Create(NotificationSeverity severity, string message, string details = "")
    {
        return new NotificationViewModel
        {
            Severity = severity,
            Message = message,
            Details = details,
        };
    }
}
