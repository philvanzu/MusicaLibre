using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicaLibre.Models;
using MusicaLibre.Services;
using DynamicData.Binding;
using DynamicData;

namespace MusicaLibre.ViewModels;

public partial class AlbumsListViewModel:LibraryDataPresenter, ISelectVirtualizableItems
{

    private List<AlbumViewModel> _albums = new();
    private ObservableCollection<AlbumViewModel> _albumsMutable = new();
    public ReadOnlyObservableCollection<AlbumViewModel>? Albums { get; set; }
    
    [ObservableProperty] private AlbumViewModel? _selectedAlbum;
    partial void OnSelectedAlbumChanged(AlbumViewModel? value)
    {
        if (!InputManager.CtrlPressed)
        {
            foreach (var album in _albums)
            {
                if(album != value && album.IsSelected)
                    album.IsSelected = false;
            }
        } 
        
        OnPropertyChanged(nameof(SelectedAlbumTracks));
        SelectedAlbums = _albums.Where(x => x.IsSelected).ToList();
        
        
        var selectedTracks = new List<Track>();
        foreach(var album in SelectedAlbums)
            selectedTracks.AddRange(album.Tracks);
        
        SelectedTracks = selectedTracks;

    }
    
    [ObservableProperty] private List<AlbumViewModel>? _selectedAlbums;
    

    public List<Track>? SelectedAlbumTracks => SelectedAlbum?.Tracks;
    
    public event EventHandler<SelectedItemChangedEventArgs>? SelectionChanged;
    public event EventHandler? SortOrderChanged;
    public event EventHandler<int>? ScrollToIndexRequested;
    public AlbumsListViewModel(LibraryViewModel library, List<Track> tracksPool)
        :base(library, tracksPool)
    {
        UpdateAlbumsCollection();
        Albums = new (_albumsMutable);
    }
    
    void UpdateAlbumsCollection()
    {
        _albums.Clear();

        
        var albumIds = TracksPool.Select(x => x.AlbumId).Where(albumId => albumId.HasValue).Distinct().ToList();

        var albumsPool = Library.Albums.Values.AsEnumerable();
        if (albumIds.Count > 0) albumsPool = albumsPool.Where(x => albumIds.Contains(x.DatabaseIndex));
        foreach (var album in albumsPool)
            _albums.Add(new AlbumViewModel(this, album));
        
        Sort();
    }
    private IComparer<AlbumViewModel> GetComparer(AlbumSortKeys sort, bool ascending)
    {
        return sort switch
        {
            AlbumSortKeys.Title => ascending
                ? SortExpressionComparer<AlbumViewModel>.Ascending(x => x.Title)
                : SortExpressionComparer<AlbumViewModel>.Descending(x => x.Title),

            AlbumSortKeys.ArtistName=>ascending
                ? SortExpressionComparer<AlbumViewModel>.Ascending(x => x.Artist)
                : SortExpressionComparer<AlbumViewModel>.Descending(x => x.Artist),

            AlbumSortKeys.Year => ascending
                ? SortExpressionComparer<AlbumViewModel>.Ascending(x => x.Year)
                : SortExpressionComparer<AlbumViewModel>.Descending(x => x.Year),

            AlbumSortKeys.RootFolder => ascending
                ? SortExpressionComparer<AlbumViewModel>.Ascending(x => x.RootFolder)
                : SortExpressionComparer<AlbumViewModel>.Descending(x => x.RootFolder),
            
            AlbumSortKeys.Added => ascending
                ? SortExpressionComparer<AlbumViewModel>.Ascending(x => x.Added.Value)
                : SortExpressionComparer<AlbumViewModel>.Descending(x => x.Added.Value),
            AlbumSortKeys.Modified => ascending
                ? SortExpressionComparer<AlbumViewModel>.Ascending(x => x.Modified.Value)
                : SortExpressionComparer<AlbumViewModel>.Descending(x => x.Modified.Value),
            AlbumSortKeys.Created => ascending
                ? SortExpressionComparer<AlbumViewModel>.Ascending(x => x.Created.Value)
                : SortExpressionComparer<AlbumViewModel>.Descending(x => x.Created.Value),
            AlbumSortKeys.LastPlayed => ascending
                ? SortExpressionComparer<AlbumViewModel>.Ascending(x => x.LastPlayed.Value)
                : SortExpressionComparer<AlbumViewModel>.Descending(x => x.LastPlayed.Value),

            AlbumSortKeys.Random => 
                SortExpressionComparer<AlbumViewModel>.Ascending(x => x.RandomIndex),

            _ => SortExpressionComparer<AlbumViewModel>.Ascending(x => x.Model.DatabaseIndex)
        };
    }

    public void ShufflePages()
    {
        foreach (var album in _albums)
            album.RandomIndex = CryptoRandom.NextInt();
    }
    public void Sort()
    {
        if(Library.CurrentStep.SortingKeys.Select(x=>  (x is SortingKey<AlbumSortKeys> sk) && sk.Key == AlbumSortKeys.Random).ToList().Count > 0)
            ShufflePages();
        
        var comparers = Library.CurrentStep.SortingKeys
            .Select(key => GetComparer((key as SortingKey<AlbumSortKeys>).Key, key.Asc))
            .ToList();

        Ascending = Library.CurrentStep.SortingKeys.First().Asc;
        var sorted = _albums.OrderBy(x => x, new CompositeComparer<AlbumViewModel>(comparers));

        _albumsMutable.Clear();
        _albumsMutable.AddRange(sorted);

        OnPropertyChanged(nameof(Albums));
        //InvokeSortOrderChanged();
    }

    public override void Reverse()
    {
        var reversed =_albumsMutable.Reverse().ToList();
        _albumsMutable.Clear();
        _albumsMutable.AddRange(reversed);
        Ascending = !Ascending;
    }

    public int GetSelectedIndex()
    {
        if(Albums == null || SelectedAlbum == null) return -1;
        return Albums.IndexOf(SelectedAlbum);
    }
    public int GetItemIndex(AlbumViewModel album)=>Albums?.IndexOf(album)??-1;

    public override NavCapsuleViewModel? GetCapsule()
    {
        if(SelectedAlbum != null)
        {
            return new NavCapsuleViewModel()
            {
                Title = SelectedAlbum.Title,
                SubTitle = SelectedAlbum.Artist,
                Artwork = SelectedAlbum.Model.Cover,
            };
        }
        return null;
    }
}