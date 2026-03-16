using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DicomViewer.Services;
using DicomViewer.ViewModels;
using DicomViewer.Views;
using FellowOakDicom;

namespace DicomViewer;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        new DicomSetupBuilder()
            .RegisterServices(s => s
                .AddFellowOakDicom()
                .AddTranscoderManager<FellowOakDicom.Imaging.NativeCodec.NativeTranscoderManager>())
            .SkipValidation()
            .Build();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = new SettingsService();
            var appSettings = settings.Load();
            LocalizationService.Instance.SetLanguage(appSettings.Language);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}