using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public partial class TrackArtworkViewModel:ViewModelBase, IDisposable, IVirtualizableItem
{
    public int Index => _manager.Artworks.IndexOf(this);
    [ObservableProperty] bool _isSelected;
    public string SourcePath {get; set;}
    public Bitmap Thumbnail { get; set; }
    private TrackArtworkManagerViewModel _manager;
    public Artwork Artwork { get; set; }
    public TrackArtworkViewModel(TrackArtworkManagerViewModel manager, Bitmap thumbnail, Artwork artwork)
    {
        _manager = manager;
        Thumbnail = thumbnail;
        Artwork = artwork;
    }

    partial void OnIsSelectedChanged(bool oldValue, bool newValue)
    {
        if(oldValue && _manager.SelectedArtwork == this)
            _manager.SelectedArtwork = null;
        if(newValue && _manager.SelectedArtwork != this)
            _manager.SelectedArtwork = this;
    }

    public void Dispose()
    {
        Thumbnail?.Dispose();
    }
    
    public bool IsFirst => Index == 0;
    public bool IsPrepared { get; set; }
    public void OnPrepared()
    {
        
    }

    public void OnCleared()
    {
        
    }
}