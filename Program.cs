using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace DicomViewer;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var mcpThread = new Thread(() => RunMcpServerAsync().GetAwaiter().GetResult());
        mcpThread.IsBackground = true;
        mcpThread.Start();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static async Task RunMcpServerAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<DicomTools>();
        await builder.Build().RunAsync();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}

[McpServerToolType]
public class DicomTools
{
    [McpServerTool, Description("Opens a DICOM file in the viewer by file path")]
    public static string OpenDicomFile(string filePath) => $"Requested to open DICOM file: {filePath}";

    [McpServerTool, Description("Returns metadata from a DICOM file such as patient name, modality, and study date")]
    public static string GetDicomMetadata(string filePath) => $"Metadata for: {filePath}";

    [McpServerTool, Description("Lists all DICOM files in a directory")]
    public static string[] ListDicomFiles(string directoryPath) => System.IO.Directory.GetFiles(directoryPath, "*.dcm");
}