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

    [ObservableProperty] bool _isSelected;

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
    [RelayCommand] void OpenInExplorer() { }

    [RelayCommand] void Delete(){}

    [RelayCommand]
    void DoubleTapped()
    {
        Presenter.Library.ChangeOrderingStep(Presenter);
    }

    

}

public partial class DiscViewModel:AlbumViewModel{
    
    public Disc Disc { get; set; }
    public override string Title => string.IsNullOrEmpty(Disc.Name)? base.Title:Disc.Name;
    public override Artwork? Artwork => Disc.Artwork ?? base.Artwork;
    
    public override List<Track> Tracks => 
        (Presenter as DiscsListViewModel)?.TracksPool
        .Where(x => x.DiscId == Disc.DatabaseIndex).ToList()
        ??new List<Track>();
    public DiscViewModel(DiscsListViewModel presenter, Disc model) : base(presenter, model.Album)
    {
        Disc = model;
    }
}