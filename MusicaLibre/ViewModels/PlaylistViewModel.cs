using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public partial class PlaylistViewModel:ViewModelBase, IVirtualizableItem
{
    [ObservableProperty] PlaylistsListViewModel _presenter;
    public List<Track> Tracks=>Model.Tracks.OrderBy(x => x.position).Select(x=>x.track).ToList();
    public string Count => $"{Tracks.Count} tracks"; 
    public Playlist Model { get; init; }
    public string FileName => Model.FileName??"";
    public string FilePath => Model.FilePath??"";
    public Bitmap? Thumbnail => Artwork?.Thumbnail;
    [ObservableProperty]Artwork? _artwork;

    public Artwork? FindArtwork()
    {
        return Presenter.Library.Artworks.Values.Where(x => x.Folder == Model.Folder).FirstOrDefault();
    }
    public int RandomIndex { get; set; }
    [ObservableProperty] private bool _isSelected;
    partial void OnIsSelectedChanged(bool oldValue, bool newValue)
    {
        if(newValue && Presenter.SelectedItem != this)
            Presenter.SelectedItem = this;
        if (oldValue && Presenter.SelectedItem == this)
            Presenter.SelectedItem = null;
    }

    public bool IsFirst => Presenter.GetItemIndex(this) == 0; 
    public bool IsPrepared { get; set; }

    public PlaylistViewModel(PlaylistsListViewModel presenter, Playlist playlist)
    {
        Model = playlist;
        Presenter = presenter;
        Artwork = FindArtwork();
    }
    public void OnPrepared()
    {
        IsPrepared = true;
        if(Artwork != null)
            Artwork.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Thumbnail)));
    }

    public void OnCleared()
    {
        if(Artwork != null)
            Artwork.ReleaseThumbnail(this);
    }

    [RelayCommand]
    void OpenInExplorer() => PathUtils.OpenInExplorer(FilePath);
    
    [RelayCommand]void Edit(){}

    [RelayCommand]void DoubleTapped()=>Presenter.Library.ChangeOrderingStep(Presenter);

    [RelayCommand] void Play()=>Presenter.Library.NowPlayingList.Replace(Tracks);
    [RelayCommand] void InsertNext()=>Presenter.Library.NowPlayingList.Insert(Tracks);
    [RelayCommand] void Append()=>Presenter.Library.NowPlayingList.Append(Tracks);
}