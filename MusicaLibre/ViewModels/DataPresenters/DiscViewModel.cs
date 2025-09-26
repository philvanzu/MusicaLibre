using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;

namespace MusicaLibre.ViewModels;

public partial class DiscViewModel:ViewModelBase, IDisposable
{
    
    [ObservableProperty] private Disc _disc;
        
    public Bitmap? Thumbnail => Disc.Artwork?.Thumbnail;

    public DiscViewModel(Disc disc)
    {
        _disc = disc;
        _disc.Artwork?.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Thumbnail)));
    }

    public void Dispose()
    {
        _disc.Artwork?.ReleaseThumbnail(this);
    }
    [RelayCommand]void Update(){}
    [RelayCommand] void PickArtwork()
    {
        
    }

}