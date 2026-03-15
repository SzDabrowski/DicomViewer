using Avalonia;
using DicomViewer.Services;
using System;
using System.Linq;

namespace DicomViewer;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        LoggingService.Instance.Info("App", "DicomViewer process starting");

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}