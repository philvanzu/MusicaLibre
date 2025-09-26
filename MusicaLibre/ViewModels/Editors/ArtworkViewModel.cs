using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using MusicaLibre.Models;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public partial class ArtworkViewModel:ViewModelBase, IDisposable, IVirtualizableItem
{
    [ObservableProperty] Artwork _artwork;
    [ObservableProperty] bool _isSelected;
    public bool IsEmbedded => Artwork.SourceType == ArtworkSourceType.Embedded;
    string SourcePath => _artwork.SourcePath;
    string Type=>EnumUtils.GetDisplayName(Artwork.SourceType);
    public Bitmap? Thumbnail => _artwork.Thumbnail;
    private ArtworkListManagerViewModel _manager;
    public ArtworkViewModel(ArtworkListManagerViewModel manager, Artwork model)
    {
        _manager = manager;
        _artwork = model;
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
        _artwork?.ReleaseThumbnail(this);
    }
    [RelayCommand]void Update(){}
    public bool IsFirst => _manager.Artworks.IndexOf(this) == 0;
    public bool IsPrepared { get; set; }
    public void OnPrepared()
    {
        _artwork.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Thumbnail)));
        //IsPrepared = true;
    }

    public void OnCleared()
    {
        _artwork.ReleaseThumbnail(this);
    }
}