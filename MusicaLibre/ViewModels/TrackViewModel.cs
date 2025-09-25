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
    private const string playString = "▶️";
    private const string pauseString = "⏸";
        
    [ObservableProperty] private bool _commentsToggle;

    private bool _playStatus; // true:playing, false:paused
    public bool PlayStatus
    {
        get => _playStatus;
        set
        {
            SetProperty(ref _playStatus, value);
            OnPropertyChanged(nameof(PlayStatusString));
        } 
    }

    public string PlayStatusString => IsPlaying ? PlayStatus ? playString : pauseString : string.Empty;
    [ObservableProperty] private Track _model; 
    Artwork? _artwork;
    
    // Core tags
    public string? Title => Model.Title;
    public string? Album => Model.Album?.Title;
    public string? Year => Model.Year?.Name??"";
    public string? Album_Year => $"{Album} - {Year}";
    public string? AlbumPosition
    {
        get
        {
            var disc = Model.DiscNumber > 0? $"{Model.DiscNumber} - ": string.Empty;
            return $"{disc}{Model.TrackNumber}";
        }
    }

    public string Artists => string.Join(", ", Model.Artists.Select((x) => x.Name));
    public int RandomIndex {get; set;}
    public string? ComposersText=> string.IsNullOrWhiteSpace(Composers)?  null: $"Composers: {Composers}";
    public string Composers=>string.Join(", ", Model.Composers.Select((x) => x.Name));
    
    public string? Remixer => Model.Remixer!= null ? $"Remixer:{Model.Remixer?.Name}":null;
    public string? Conductor => Model.Conductor!= null? $"Conductor: {Model.Conductor?.Name}":null;
    public string Genres => string.Join(", ", Model.Genres.Select((x) => x.Name));
    public TimeSpan? TrackDuration => (Model.End - Model.Start) * Model.Duration;
    public TimeSpan? FileDuration_ => Model.Duration;
    public string Duration => TimeUtils.FormatDuration(TrackDuration.Value);
    
    
    public string Added => TimeUtils.FormatDate(Model.DateAdded);
    public string AddedFull => TimeUtils.FormatDateTime(Model.DateAdded);
    public string Modified => TimeUtils.FormatDate(Model.Modified);
    public string ModifiedFull => TimeUtils.FormatDateTime(Model.Modified);
    public string Created => TimeUtils.FormatDate(Model.Created);
    public string CreatedFull => TimeUtils.FormatDateTime(Model.Created);
    public string Played => TimeUtils.FormatDate(Model.LastPlayed);
    public string PlayedFull => TimeUtils.FormatDateTime(Model.LastPlayed);
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

    public Bitmap? Thumbnail => _artwork?.Thumbnail;
    
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
            
            OnPropertyChanged(nameof(PlayStatusString));
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
            if (newValue && Presenter.SelectedItem != this)
                Presenter.SelectedItem = this;
            else if (oldValue && Presenter.SelectedItem == this)
                Presenter.SelectedItem = null;
        }
        finally
        {
            _suppressSelectedUpdates = false;
        }
    }

    public bool FileIsMultitrack => Model.Start != 0 || Model.End != 1;
    public bool IsLastMultitrackFileTrack => Model.End == 1;
    public TracksListViewModel Presenter { get; set; }
    
    public TrackViewModel(Track model, TracksListViewModel presenter)
    {
        _model = model;
        Presenter = presenter;
    }

    public double FileToTrackPosition(double filePosition)
    {
        if (Model.Start == 0 && Model.End == 1)
            return filePosition;

        double range = Model.End - Model.Start;
        if (range <= 0)
            return 0; // invalid cue, avoid div-by-zero

        double pos = (filePosition - Model.Start) / range;
        return Math.Clamp(pos, 0, 1);
    }

    public double TrackToFilePosition(double trackPosition)
    {
        if (Model.Start == 0 && Model.End == 1)
            return trackPosition;

        double range = Model.End - Model.Start;
        return Math.Clamp(Model.Start + trackPosition * range, 0, 1);
    }

    
    public void GetThumbnail()
    {
        _artwork = Model.Artworks.FirstOrDefault() ?? Model.Album?.Cover;
        _artwork?.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Thumbnail)));
    }

    public void ReleaseThumbnail()
    {
        _artwork?.ReleaseThumbnail(this);
        _artwork = null;
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
    [RelayCommand] void EditTags()=>_ = Presenter.Library.EditTracks(Presenter.SelectedTracks);
    [RelayCommand] void Transcode()=>_ = Presenter.Library.TranscodeTracks(Presenter.SelectedTracks);
    [RelayCommand] void OpenInExplorer(){PathUtils.OpenInExplorer(Model.FilePath);}

    [RelayCommand] void ShowInfo() => CommentsToggle = false;
    [RelayCommand] void ShowComments()=> CommentsToggle = true;


}