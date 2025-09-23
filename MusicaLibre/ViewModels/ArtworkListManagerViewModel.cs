using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicaLibre.Models;

namespace MusicaLibre.ViewModels;

public abstract partial class ArtworkListManagerViewModel:ViewModelBase, IDisposable, ISelectVirtualizableItems
{
    [ObservableProperty] private string? _selectionStr;
    [ObservableProperty] private ObservableCollection<ArtworkViewModel> _artworks = new();
    [ObservableProperty] private ArtworkViewModel? _selectedArtwork;
    [ObservableProperty] private ProgressViewModel _progress = new ProgressViewModel();
    
    public event EventHandler<SelectedItemChangedEventArgs>? SelectionChanged;
    public event EventHandler? SortOrderChanged;
    public event EventHandler<int>? ScrollToIndexRequested;

    protected abstract void SelectedArtworkChanged(ArtworkViewModel? value);
    partial void OnSelectedArtworkChanged(ArtworkViewModel? value)
    {
        SelectedArtworkChanged(value);
    }

    public void Dispose()
    {
        foreach (var item in _artworks)
            item.Dispose();
    }

    public int GetItemIndex(ArtworkViewModel artwork)=>Artworks.IndexOf(artwork);
    public int GetSelectedIndex() => SelectedArtwork != null ? Artworks.IndexOf(SelectedArtwork) : -1;

}