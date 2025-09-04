using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicaLibre.ViewModels;

public partial class NavigatorViewModel<T>:ViewModelBase
{
    [ObservableProperty] private LibraryViewModel _library;
    private readonly Stack<T> _back = new();
    public bool IsVisible => CanGoBack;
    public T? Current { get; private set; }

    public NavigatorViewModel(LibraryViewModel library, T home)
    {
        Library = library;
        Current = home;
    }

    public void Navigate(T item)
    {
        if (Current != null)
            _back.Push(Current);

        Current = item;
        Refresh();
    }

    void Refresh()
    {
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsVisible));
    }

    public bool CanGoBack => _back.Count > 0;

    public void GoBack()
    {
        if (!CanGoBack) return;

        Current = _back.Pop();
        Refresh();
    }


    public T? PeekBack => _back.Peek();
}