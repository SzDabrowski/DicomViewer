using CommunityToolkit.Mvvm.ComponentModel;
using DicomViewer.Services;

namespace DicomViewer.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Exposes the localization service so XAML bindings via DataContext
    /// properly subscribe to INotifyPropertyChanged and update on language switch.
    /// Usage in XAML: {Binding Loc[Key]}
    /// </summary>
    public LocalizationService Loc => LocalizationService.Instance;
}
