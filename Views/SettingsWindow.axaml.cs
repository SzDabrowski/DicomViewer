using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DicomViewer.ViewModels;

namespace DicomViewer.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
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

        var closeBtn = this.FindControl<Button>("CloseBtn");
        if (closeBtn != null)
            closeBtn.Click += (_, _) => Close();

        if (DataContext is SettingsViewModel vm)
            vm.RequestClose = () => Close();
    }
}
