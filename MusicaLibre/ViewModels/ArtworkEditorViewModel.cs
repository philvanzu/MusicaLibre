using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using MusicaLibre.Models;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public partial class ArtworkEditorViewModel:ViewModelBase, IDisposable
{
    [ObservableProperty] Artwork _artwork;
    string SourcePath => _artwork.SourcePath;
    string Type=>EnumUtils.GetDisplayName(Artwork.SourceType);
    Bitmap? Thumbnail => _artwork.Thumbnail;

    public ArtworkEditorViewModel(Artwork model, Artwork artwork, bool editorMode=true)
    {
        _artwork = model;
        _artwork.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Thumbnail)));
        
    }

    public void Dispose()
    {
        _artwork?.ReleaseThumbnail(this);
    }
    [RelayCommand]void Update(){}
}