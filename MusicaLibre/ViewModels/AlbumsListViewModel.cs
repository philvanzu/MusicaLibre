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

    protected List<AlbumViewModel> _items = new();
    protected ObservableCollection<AlbumViewModel> _itemsMutable = new();
    public ReadOnlyObservableCollection<AlbumViewModel>? Items { get; set; }
    
    [ObservableProperty] private AlbumViewModel? _selectedItem;
    private AlbumViewModel? _shiftSelectionAnchor;
    partial void OnSelectedItemChanged(AlbumViewModel? value)
    {
        if (!InputManager.CtrlPressed && !InputManager.IsDragSelecting && !InputManager.ShiftPressed)
        {
            foreach (var item in _items)
            {
                item.IsSelected = item == value;
            }    
        }
        
        if (Items != null && InputManager.ShiftPressed && _shiftSelectionAnchor != null && value != null){
            var startIdx = GetItemIndex(_shiftSelectionAnchor);
            var endIdx = GetItemIndex(value);
            var step = startIdx <= endIdx ? 1 : -1;
            for (var i = startIdx; i != endIdx + step; i += step)
                Items[i].IsSelected = true;
        }
        
        if(!InputManager.ShiftPressed) 
            _shiftSelectionAnchor = value;
        
        OnPropertyChanged(nameof(SelectedItemTracks));
        SelectedItems = _items.Where(x => x.IsSelected).ToList();
        
        
        var selectedTracks = new List<Track>();
        foreach(var album in SelectedItems)
            selectedTracks.AddRange(album.Tracks);
        
        SelectedTracks = selectedTracks;

    }
    
    [ObservableProperty] protected List<AlbumViewModel>? _selectedItems;
    

    public List<Track>? SelectedItemTracks => SelectedItem?.Tracks;
    
    public event EventHandler<SelectedItemChangedEventArgs>? SelectionChanged;
    public event EventHandler? SortOrderChanged;
    public event EventHandler<int>? ScrollToIndexRequested;
    public AlbumsListViewModel(LibraryViewModel library, List<Track> tracksPool)
        :base(library, tracksPool)
    {
        UpdateItemsCollection();
        Items = new (_itemsMutable);
    }
    
    protected virtual void UpdateItemsCollection()
    {
        _items.Clear();

        
        var albumIds = TracksPool.Select(x => x.AlbumId).Where(albumId => albumId.HasValue).Distinct().ToList();

        var albumsPool = Library.Albums.Values.AsEnumerable();
        if (albumIds.Count > 0) albumsPool = albumsPool.Where(x => albumIds.Contains(x.DatabaseIndex));
        foreach (var album in albumsPool)
            _items.Add(new AlbumViewModel(this, album));
        
        Sort();
    }
    protected IComparer<AlbumViewModel> GetComparer(AlbumSortKeys sort, bool ascending)
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
        foreach (var item in _items)
            item.RandomIndex = CryptoRandom.NextInt();
    }

    public override void Filter(string searchString)
    {
        if (string.IsNullOrWhiteSpace(searchString))
        {
            Sort();
            return;
        }
        Dictionary<Track, double> weights = SearchUtils.FilterTracks( searchString, TracksPool, Library);
        var albumWeights = new Dictionary<Album, double>();
        foreach (var kv in weights)
        {
            var album = kv.Key.Album;
            if (album == null) continue;
            albumWeights[album] = albumWeights.GetValueOrDefault(album) + kv.Value;
        }

        var filtered = _items.Where(x => albumWeights.GetValueOrDefault(x.Model) > 0);
        var ordered = filtered.OrderByDescending(x => albumWeights.GetValueOrDefault(x.Model));
        _itemsMutable.Clear();
        _itemsMutable.AddRange(ordered);

        OnPropertyChanged(nameof(Items));
    }
    
    public virtual void Sort()
    {
        if (!string.IsNullOrWhiteSpace(Library.SearchString))
        {
            Filter(Library.SearchString);
            return;
        }
        if(Library.CurrentStep.SortingKeys.Select(x=>  (x is SortingKey<AlbumSortKeys> sk) && sk.Key == AlbumSortKeys.Random).ToList().Count > 0)
            ShufflePages();
        
        var comparers = Library.CurrentStep.SortingKeys
            .Select(key => GetComparer((key as SortingKey<AlbumSortKeys>).Key, key.Asc))
            .ToList();

        Ascending = Library.CurrentStep.SortingKeys.First().Asc;
        var sorted = _items.OrderBy(x => x, new CompositeComparer<AlbumViewModel>(comparers));

        _itemsMutable.Clear();
        _itemsMutable.AddRange(sorted);

        OnPropertyChanged(nameof(Items));
        //InvokeSortOrderChanged();
    }

    public override void Reverse()
    {
        var reversed =_itemsMutable.Reverse().ToList();
        _itemsMutable.Clear();
        _itemsMutable.AddRange(reversed);
        Ascending = !Ascending;
    }

    public int GetSelectedIndex()
    {
        if(Items == null || SelectedItem == null) return -1;
        return Items.IndexOf(SelectedItem);
    }
    public int GetItemIndex(AlbumViewModel album)=>Items?.IndexOf(album)??-1;

    public override NavCapsuleViewModel? GetCapsule()
    {
        if(SelectedItem != null)
        {
            return new NavCapsuleViewModel()
            {
                Title = SelectedItem.Title,
                SubTitle = SelectedItem.Artist,
                Artwork = SelectedItem.Artwork,
            };
        }
        return null;
    }
}

public partial class DiscsListViewModel : AlbumsListViewModel
{
    public DiscsListViewModel(LibraryViewModel library, List<Track> tracksPool) : base(library, tracksPool)
    {
    }

    protected override void UpdateItemsCollection()
    {
        _items.Clear();

        
        var discIds = TracksPool.Select(x => (x.DiscNumber, x.AlbumId)).Distinct().ToList();

        var discsPool = Library.Discs.Values.AsEnumerable();
        if (discIds.Count > 0) discsPool = discsPool.Where(x => discIds.Contains((x.Number, x.AlbumId)));
        foreach (var disc in discsPool)
            _items.Add(new AlbumDiscViewModel(this, disc));
        
        Sort();
    }

}