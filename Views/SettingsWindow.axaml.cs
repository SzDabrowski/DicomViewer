using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DicomViewer.ViewModels;

namespace DicomViewer.Views;

public partial class SettingsWindow : Window
{
    private KeyBindingRowViewModel? _recordingRow;

    public SettingsWindow()
    {
        InitializeComponent();

        AddHandler(KeyDownEvent, OnKeyBindingCapture, RoutingStrategies.Tunnel);
        AddHandler(Button.ClickEvent, OnKeyBindingButtonClick);
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

    private void OnKeyBindingButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button button && button.Tag is KeyBindingRowViewModel row)
        {
            _recordingRow?.CancelRecording();
            row.StartRecording();
            _recordingRow = row;
        }
    }

    private void OnKeyBindingCapture(object? sender, KeyEventArgs e)
    {
        if (_recordingRow == null)
            return;

        if (e.Key == Key.Escape)
        {
            _recordingRow.CancelRecording();
        }
        else
        {
            _recordingRow.ApplyKey(e.Key, e.KeyModifiers);
        }

        _recordingRow = null;
        e.Handled = true;
    }
}
