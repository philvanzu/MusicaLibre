using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public partial class TrackViewModel:ViewModelBase, IVirtualizableItem
{
    [ObservableProperty] private Track _model; 
    
    // Core tags
    public string? Title => Model.Title;
    public string? Album => Model.Album?.Title;
    public string? Year => Model.Year?.Name??"";
    public string? Album_Year => $"{Album} - {Year}";
    public string Artists => string.Join(", ", Model.Artists.Select((x) => x.Name));
    public int RandomIndex {get; set;}
    public string? Composers
    {
        get
        {
            var composers = string.Join(", ", Model.Composers.Select((x) => x.Name));
            if (string.IsNullOrWhiteSpace(composers)) return null;
            return $"Composers: {composers}";
        }
    } 
    public string? Remixer => Model.Remixer!= null ? $"Remixer:{Model.Remixer?.Name}":null;
    public string? Conductor => Model.Conductor!= null? $"Conductor: {Model.Conductor?.Name}":null;
    public string Genres => string.Join(", ", Model.Genres.Select((x) => x.Name));
    
    public string Duration => TimeUtils.FormatDuration(Model.Duration.Value);
    public string Added => TimeUtils.FormatDate(Model.DateAdded);
    public string Modified => TimeUtils.FormatDate(Model.Modified);
    public string Created => TimeUtils.FormatDate(Model.Created);
    public string Played => TimeUtils.FormatDate(Model.LastPlayed);
    public string? Publisher => Model.Publisher!= null? $"Publisher : {Model.Publisher.Name}":null;
    public string Bitrate => $"{Model.BitrateKbps}Kbps";
    
    public string TechInfo
    {
        get
        {
            var ext = Path.GetExtension(Model.FileName);
            var ret = $"{ext} | {Model.BitrateKbps}kbps | {Model.SampleRate}khz";
            return ret;
        }
    }

    public Bitmap? Artwork => Model.Artworks.FirstOrDefault()?.Thumbnail;
    
    public bool EvenRow=> Presenter.GetItemIndex(this) % 2 == 0;
    
    [ObservableProperty] private int _nowPlayingIndex;
    [ObservableProperty] private bool _isPlaying;
    partial void OnIsPlayingChanged(bool oldValue, bool newValue)
    {
        if (Presenter is NowPlayingListViewModel nowPlayingListVM)
        {
            if(newValue && nowPlayingListVM.PlayingTrack != this)
                nowPlayingListVM.PlayingTrack = this;
            else if(oldValue && nowPlayingListVM.PlayingTrack == this)
                nowPlayingListVM.PlayingTrack = null;
        }
        
    }

    private bool _suppressSelectedUpdates;
    [ObservableProperty] private bool _isSelected;
    partial void OnIsSelectedChanged(bool oldValue, bool newValue)
    {
        if (_suppressSelectedUpdates) return;
        try
        {
            _suppressSelectedUpdates = true;
            if (newValue && Presenter.SelectedTrack != this)
                Presenter.SelectedTrack = this;
            else if (oldValue && Presenter.SelectedTrack == this)
                Presenter.SelectedTrack = null;
        }
        finally
        {
            _suppressSelectedUpdates = false;
        }
    }

    public TracksListViewModel Presenter { get; set; }
    public TrackViewModel(Track model, TracksListViewModel presenter)
    {
        _model = model;
        Presenter = presenter;
    }
    
    public void GetThumbnail()
    {
        Model.Artworks.FirstOrDefault()?.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Artwork)));
    }

    public void ReleaseThumbnail()
    {
        Model.Artworks.FirstOrDefault()?.ReleaseThumbnail(this);
    }

    public bool IsFirst => Presenter.GetItemIndex(this) == 0;
    public bool IsPrepared { get; private set; }
    public int PlaylistPosition { get; set; }

    public void OnPrepared()
    {
        IsPrepared = true;
        if(Presenter is NowPlayingListViewModel nowPlayingListVM)
            GetThumbnail();
    }

    public void OnCleared()
    {
        if(Presenter is NowPlayingListViewModel nowPlayingListVM)
            ReleaseThumbnail();
    }


    [RelayCommand] void DoubleTapped() => Presenter.Library.NowPlayingList.Replace(Presenter.SelectedTracks);
    [RelayCommand] void Append() => Presenter.Library.NowPlayingList.Append(Presenter.SelectedTracks);
    [RelayCommand] void InsertNext()=> Presenter.Library.NowPlayingList.Insert(Presenter.SelectedTracks);
    [RelayCommand] void EditTags(){}
    [RelayCommand] void OpenInExplorer(){}
}