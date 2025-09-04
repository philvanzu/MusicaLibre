using System.Collections.Generic;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicaLibre.Models;

namespace MusicaLibre.ViewModels;

public partial class NavCapsuleViewModel:ViewModelBase
{
    public string Title { get; set; }
    public string SubTitle { get; set; }
    [ObservableProperty] private string _currentOrderingText; 
    Artwork _artwork;

    public Artwork? Artwork { get; set; }
    public Bitmap? Thumbnail => Artwork?.Thumbnail;
    

    public void Register()
    {
        if(Artwork != null) 
            Artwork.RequestThumbnail(this, 
                ()=>OnPropertyChanged(nameof(Thumbnail)));
    }

    public void Release()
    {
        if(Artwork != null)
            Artwork.ReleaseThumbnail(this);
    }
}