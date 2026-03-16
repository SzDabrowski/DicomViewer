using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using IconPacks.Avalonia.Codicons;

namespace DicomViewer.Views;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        var dragArea = this.FindControl<Border>("TitleBarDragArea");
        if (dragArea != null)
            dragArea.PointerPressed += (_, args) =>
            {
                if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(args);
            };

        this.FindControl<Button>("BtnMinimise")!.Click += (_, _)
            => WindowState = WindowState.Minimized;

        var maxIcon = this.FindControl<PackIconCodicons>("MaximiseIcon")!;
        this.FindControl<Button>("BtnMaximise")!.Click += (_, _) =>
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            maxIcon.Kind = WindowState == WindowState.Maximized
                ? PackIconCodiconsKind.ChromeRestore
                : PackIconCodiconsKind.ChromeMaximize;
        };

        this.FindControl<Button>("CloseBtn")!.Click += (_, _) => Close();
    }
}
