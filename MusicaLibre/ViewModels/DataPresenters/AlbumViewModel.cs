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
    public virtual string Title => Model.Title;
    public string Artist => Model.AlbumArtist?.Name??"";
    public string Year => Model.Year?.Name ?? "";

    public string RootFolder => Model.Folder.Name??"";
    public DateTime? Added => Model.Added;
    public DateTime? Modified => Model.Modified;
    public DateTime? Created => Model.Created;
    public DateTime? LastPlayed => Model.LastPlayed;
    public int RandomIndex {get;set;}
    public virtual List<Track> Tracks => Presenter.TracksPool.Where(x=>x.AlbumId == Model.DatabaseIndex).ToList();

    public virtual Artwork? Artwork => Model.Cover;
    public Bitmap? Thumbnail => Artwork?.Thumbnail;

    private bool _suppressSelectedUpdates;
    [ObservableProperty] bool _isSelected;
    partial void OnIsSelectedChanged(bool oldValue, bool newValue)
    {
        if (_suppressSelectedUpdates) return;
        try
        {
            _suppressSelectedUpdates = true;   
            if (newValue && Presenter.SelectedItem != this )
                Presenter.SelectedItem = this;
            else if (oldValue && Presenter.SelectedItem == this)
                Presenter.SelectedItem = null;
        }
        finally
        {
            _suppressSelectedUpdates = false;
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

    

    [RelayCommand] void Play()=>Presenter.Library.NowPlayingList.Replace(Tracks);
    [RelayCommand] void InsertNext()=>Presenter.Library.NowPlayingList.Insert(Tracks);
    [RelayCommand] void Append()=>Presenter.Library.NowPlayingList.Append(Tracks);
    [RelayCommand] void OpenInExplorer() { PathUtils.OpenInExplorer(Model.Folder.Name);}

    [RelayCommand] void Delete(){}

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

    [RelayCommand]
    void DoubleTapped()
    {
        Presenter.Library.ChangeOrderingStep(Presenter);
    }

    

}

public partial class AlbumDiscViewModel:AlbumViewModel{
    
    public Disc Disc { get; set; }
    public override string Title => string.IsNullOrEmpty(Disc.Name)? base.Title:Disc.Name;
    public override Artwork? Artwork => Disc.Artwork ?? base.Artwork;
    
    public override List<Track> Tracks => 
        (Presenter as DiscsListViewModel)?.TracksPool
        .Where(x => x.DiscNumber == Disc.Number && x.AlbumId == Disc.AlbumId).ToList()
        ??new List<Track>();
    public AlbumDiscViewModel(DiscsListViewModel presenter, Disc model) : base(presenter, model.Album)
    {
        Disc = model;
    }
}