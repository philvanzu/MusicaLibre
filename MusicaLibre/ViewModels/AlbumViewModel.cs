using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.ViewModels;

public partial class AlbumViewModel : ViewModelBase, IVirtualizableItem
{
    public Album Model { get; init; }
    public string Title => Model.Title;
    public string Artist => Model.AlbumArtist?.Name??"";
    public string Year => Model.Year?.Name ?? "";

    public string RootFolder => Model.Folder.Name??"";
    public DateTime? Added => Model.Added;
    public DateTime? Modified => Model.Modified;
    public DateTime? Created => Model.Created;
    public DateTime? LastPlayed => Model.LastPlayed;
    public int RandomIndex {get;set;}
    public List<Track> Tracks => Presenter.TracksPool.Where(x=>x.AlbumId == Model.DatabaseIndex).ToList();
    public Bitmap? Thumbnail => Model.Cover?.Thumbnail;

    [ObservableProperty] bool _isSelected;

    partial void OnIsSelectedChanged(bool oldValue, bool newValue)
    {
        if (newValue && Presenter.SelectedAlbum != this )
        {
            Presenter.SelectedAlbum = this;
            Console.WriteLine($"{Title} is selected");
        }
        else if (oldValue && Presenter.SelectedAlbum == this)
        {
            Presenter.SelectedAlbum = null;
            Console.WriteLine($"{Title} is unselected");
        }
    }

    public AlbumsListViewModel Presenter { get; init; }
    public bool IsFirst => Presenter.GetItemIndex(this) == 0;
    public bool IsPrepared { get; private set; }

    public AlbumViewModel(AlbumsListViewModel presenter, Album model)
    {
        Presenter = presenter;
        Model = model;
    }

    public void OnPrepared()
    {
        IsPrepared = true;
        GetThumbnail();
    }

    public void OnCleared()
    {
        Model.Cover?.ReleaseThumbnail(this);
    }

    public void GetThumbnail()
    {
        
        if (Model.Cover == null) return;
        Model.Cover.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Thumbnail)));

        
    }

    

    [RelayCommand]
    void Play()
    {
        List<TrackViewModel> vms = new List<TrackViewModel>();
        var ordered = Tracks.OrderBy(x => x.DiskNumber).ThenBy(x => x.TrackNumber);
        Presenter.Library.NowPlayingList.Replace(ordered.ToList());
    }
    [RelayCommand] void OpenInExplorer() { }

    [RelayCommand] void Delete(){}

    [RelayCommand]
    void DoubleTapped()
    {
        Presenter.Library.ChangeOrderingStep(Presenter);
    }

    

}