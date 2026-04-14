using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DicomViewer.Services;

namespace DicomViewer.ViewModels;

public abstract partial class ViewModelBase : ObservableObject, IDisposable
{
    /// <summary>
    /// Exposes the localization service so XAML bindings via DataContext
    /// properly subscribe to INotifyPropertyChanged and update on language switch.
    /// Usage in XAML: {Binding Loc[Key]}
    /// </summary>
    public LocalizationService Loc => LocalizationService.Instance;

    private readonly PropertyChangedEventHandler _locChangedHandler;
    private bool _disposed;

    protected ViewModelBase()
    {
        // Forward language change notifications so {Binding Loc[Key]} re-evaluates.
        // Store handler reference so we can unsubscribe in Dispose to prevent memory leaks.
        _locChangedHandler = (_, e) =>
        {
            if (e.PropertyName == "Item[]" || e.PropertyName == nameof(LocalizationService.CurrentLanguage))
            {
                OnPropertyChanged(string.Empty); // signal all properties changed so Avalonia re-evaluates {Binding Loc[Key]} even though Loc returns the same singleton reference
                OnLanguageChanged();
            }
        };
        LocalizationService.Instance.PropertyChanged += _locChangedHandler;
    }

    /// <summary>
    /// Called whenever the active language changes. Override in subclasses to refresh
    /// C#-assigned localized properties that cannot use {Binding Loc[Key]} directly.
    /// </summary>
    protected virtual void OnLanguageChanged() { }

    /// <summary>
    /// Unsubscribes from the localization service to prevent memory leaks.
    /// Subclasses that override should call base.Dispose().
    /// </summary>
    public virtual void Dispose()
    {
        if (!_disposed)
        {
            LocalizationService.Instance.PropertyChanged -= _locChangedHandler;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
