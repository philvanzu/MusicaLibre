using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public abstract partial class NameTagViewModelBase : ViewModelBase, IVirtualizableItem
{
    [ObservableProperty] protected NameTagsPresenterViewModelBase _presenter;
    [ObservableProperty] protected NameTag _model;
    public List<Track> Tracks => GetTracks();
    public abstract List<Track> GetTracks();
    public virtual string Title => _model.Name??"";
    public virtual string Count => $"{Tracks.Count} tracks";
    public virtual string SubTitle => Count;
    Artwork? _artwork;

    public Artwork? Artwork
    {
        get
        {
            if (Model.Artwork != null) return Model.Artwork;
            else if (_artwork != null) return _artwork;
            else _artwork = FindArtwork();
            return _artwork;
        }
    }
    
    public long DatabaseIndex => _model.DatabaseIndex;
    public int RandomIndex { get; set; }
    public Bitmap? Thumbnail => Artwork?.Thumbnail;
    [ObservableProperty] private bool _isSelected;
    partial void OnIsSelectedChanged(bool oldValue, bool newValue)
    {
        if (newValue && Presenter.SelectedItem != this )
        {
            Presenter.SelectedItem = this;
            Console.WriteLine($"{Title} is selected");
        }
        else if (oldValue && Presenter.SelectedItem == this)
        {
            Presenter.SelectedItem = null;
            Console.WriteLine($"{Title} is unselected");
        }
    }
    public bool IsFirst => Presenter.GetIndex(this) == 0;
    public bool IsPrepared { get; private set; }

    public NameTagViewModelBase(NameTag model, NameTagsPresenterViewModelBase presenter)
    {
        _presenter = presenter;
        _model = model;
        
    }
    Artwork? FindArtwork()
    {
        //Find a random album cover in the selected tracks
        int tries = 0;
        while (tries < 10)
        {
            int randomIdx = -1;
            if( Tracks.Count > 1)
                randomIdx = CryptoRandom.NextInt(0, Tracks.Count - 1);
            else if (Tracks.Count == 1) randomIdx = 0;
            if (randomIdx > -1)
            {
                var albumcover = Tracks[randomIdx].Album?.Cover;
                if ( albumcover != null) return albumcover;
            }
            tries++;
        }

        return null;
    }
    public void OnPrepared()
    {
        IsPrepared = true;
        if(Artwork != null)Artwork.RequestThumbnail(this, 
            ()=>OnPropertyChanged(nameof(Thumbnail)));
            
    }

    public void OnCleared()
    {
        if(Artwork != null)
            Artwork.ReleaseThumbnail(this);
    }

    [RelayCommand] void Edit()
    {
        
    }
    [RelayCommand]private void DoubleTapped()
    {
        Presenter.Library.ChangeOrderingStep(Presenter);
    }

    [RelayCommand]private void Play()=>Presenter.Library.NowPlayingList.Replace(Tracks);
    [RelayCommand]private void InsertNext()=>Presenter.Library.NowPlayingList.Insert(Tracks);
    [RelayCommand]private void Append()=>Presenter.Library.NowPlayingList.Append(Tracks);
    [RelayCommand]
    void EditTags()
    {
        if (Presenter.SelectedTracks != null && Presenter.SelectedTracks.Count > 0)
            _ = Presenter.Library.EditTracks(Presenter.SelectedTracks);
        else
            _ = Presenter.Library.EditTracks(Tracks);

    }

    [RelayCommand]
    void Transcode()
    {
        if (Presenter.SelectedTracks != null && Presenter.SelectedTracks.Count > 0)
            _ = Presenter.Library.TranscodeTracks(Presenter.SelectedTracks);
        else
            _ = Presenter.Library.TranscodeTracks(Tracks);
    }
}
public partial class NameTagViewModel<T>:NameTagViewModelBase where T:NameTag
{
    public override string Title=>typeof(T)==typeof(Folder) ? Path.GetRelativePath(Presenter.Library.Path, base.Title):base.Title;
    private Func<List<Track>, T , List<Track>> TracksDelegate { get; init; }

    public NameTagViewModel(T model, NameTagsListViewModel<T> presenter, Func<List<Track>, T, List<Track>> tracksDelegate)
    : base(model, presenter)
    {
        TracksDelegate = tracksDelegate;
    }
    
    public override List<Track> GetTracks() =>TracksDelegate.Invoke(Presenter.TracksPool, Model as T);
   
}