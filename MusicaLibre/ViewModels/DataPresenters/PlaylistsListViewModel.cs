using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;
using MusicaLibre.Models;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public partial class PlaylistsListViewModel : LibraryDataPresenter, ISelectVirtualizableItems
{
   private List<PlaylistViewModel> _items = new();
    private ObservableCollection<PlaylistViewModel> _itemsMutable = new();
    public ReadOnlyObservableCollection<PlaylistViewModel>? Items { get; set; }
    
    [ObservableProperty] private PlaylistViewModel? _selectedItem;
    partial void OnSelectedItemChanged(PlaylistViewModel? value)
    {
        if (!InputManager.CtrlPressed)
        {
            foreach (var playlist in _items)
            {
                if(playlist != value && playlist.IsSelected)
                    playlist.IsSelected = false;
            }
        } 
        
        OnPropertyChanged(nameof(SelectedItemTracks));
        SelectedItems = _items.Where(x => x.IsSelected).ToList();
        
        
        var selectedTracks = new List<Track>();
        foreach(var item in SelectedItems)
            selectedTracks.AddRange(item.Tracks);
        
        SelectedTracks = selectedTracks;

    }
    
    [ObservableProperty] private List<PlaylistViewModel>? _selectedItems;
    

    public List<Track>? SelectedItemTracks => SelectedItem?.Tracks;
    
    public event EventHandler<SelectedItemChangedEventArgs>? SelectionChanged;
    public event EventHandler? SortOrderChanged;
    public event EventHandler<int>? ScrollToIndexRequested;
    public Action<double>? ScrollToOffset;
    public double ScrollOffset {get; set;}
    
    public PlaylistsListViewModel(LibraryViewModel library, List<Track> tracksPool)
        :base(library, tracksPool)
    {
        UpdateCollection();
        Items = new (_itemsMutable);
        _initialized = true;
    }
    
    void UpdateCollection()
    {
        _items.Clear();


        var playlistIds = Library.Data.Playlists.Values
            .Where(x => x.Tracks.Select(pair => pair.track).Intersect(_tracksPool).Any())
            .Select(x => x.DatabaseIndex)
            .Distinct()
            .ToList();

        var playlistsPool = AppData.Instance.UserSettings.FilterOutEmptyPlaylists?
                Library.Data.Playlists.Values.Where(x => x.Tracks.Count > 0)
                :Library.Data.Playlists.Values;
        if (playlistIds.Count > 0) playlistsPool = playlistsPool.Where(x => playlistIds.Contains(x.DatabaseIndex));
        foreach (var item in playlistsPool)
            _items.Add(new PlaylistViewModel(this, item));
        
        Sort();
    }
    private IComparer<PlaylistViewModel> GetComparer(PlaylistSortKeys sort, bool ascending)
    {
        return sort switch
        {
            PlaylistSortKeys.Name => ascending
                ? SortExpressionComparer<PlaylistViewModel>.Ascending(x => x.FileName)
                : SortExpressionComparer<PlaylistViewModel>.Descending(x => x.FileName),

            PlaylistSortKeys.Path=>ascending
                ? SortExpressionComparer<PlaylistViewModel>.Ascending(x => x.FilePath)
                : SortExpressionComparer<PlaylistViewModel>.Descending(x => x.FilePath),

            PlaylistSortKeys.Added => ascending
                ? SortExpressionComparer<PlaylistViewModel>.Ascending(x => x.Model.Added)
                : SortExpressionComparer<PlaylistViewModel>.Descending(x => x.Model.Added),
            PlaylistSortKeys.Modified => ascending
                ? SortExpressionComparer<PlaylistViewModel>.Ascending(x => x.Model.Modified)
                : SortExpressionComparer<PlaylistViewModel>.Descending(x => x.Model.Modified),
            PlaylistSortKeys.Created => ascending
                ? SortExpressionComparer<PlaylistViewModel>.Ascending(x => x.Model.Created)
                : SortExpressionComparer<PlaylistViewModel>.Descending(x => x.Model.Created),
            PlaylistSortKeys.LastPlayed => ascending
                ? SortExpressionComparer<PlaylistViewModel>.Ascending(x => x.Model.Played.Value )
                : SortExpressionComparer<PlaylistViewModel>.Descending(x => x.Model.Played.Value),

            PlaylistSortKeys.Random => 
                SortExpressionComparer<PlaylistViewModel>.Ascending(x => x.RandomIndex),

            _ => SortExpressionComparer<PlaylistViewModel>.Ascending(x => x.Model.DatabaseIndex)
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
        
        var playlistWeights = new Dictionary<PlaylistViewModel, double>();
        foreach (var item in _items)
        {
            double w = 0;
            foreach(var track in item.Tracks)
                w += weights.GetValueOrDefault(track);
            if(w > 0)
                playlistWeights.Add(item, w);
        }
        var filtered = _items.Where(x => playlistWeights.GetValueOrDefault(x) > 0);
        var ordered = filtered.OrderByDescending(x => playlistWeights.GetValueOrDefault(x));
        _itemsMutable.Clear();
        _itemsMutable.AddRange(ordered);

        OnPropertyChanged(nameof(Items));
    }
    
    public void Sort()
    {
        if (!string.IsNullOrWhiteSpace(Library.SearchString))
        {
            Filter(Library.SearchString);
            return;
        }
        if(Library.CurrentStep.SortingKeys.Select(x=>  (x is SortingKey<PlaylistSortKeys> sk) && sk.Key == PlaylistSortKeys.Random).Any())
            ShufflePages();
        
        var comparers = Library.CurrentStep.SortingKeys
            .Select(key => GetComparer((key as SortingKey<PlaylistSortKeys>).Key, key.Asc))
            .ToList();

        Ascending = Library.CurrentStep.SortingKeys.First().Asc;
        var sorted = _items.OrderBy(x => x, new CompositeComparer<PlaylistViewModel>(comparers));

        _itemsMutable.Clear();
        _itemsMutable.AddRange(sorted);

        OnPropertyChanged(nameof(Items));
        //InvokeSortOrderChanged();
    }

    public override void Refresh()
    {
        long? selectedId=null;
        if (SelectedItem != null)
            selectedId = SelectedItem.Model.DatabaseIndex;

        List<long> selectedIds = new List<long>();
        if (SelectedItems != null)
            foreach (var item in SelectedItems)
                selectedIds.Add(item.Model.DatabaseIndex);

        var offset = ScrollOffset;
        UpdateCollection();
        PlaylistViewModel? selected = null;
        if (Items != null)
        {
            foreach (var item in Items)
            {
                if (selectedIds.Contains(item.Model.DatabaseIndex))
                    item.IsSelected = true;
                
                if(selectedId.HasValue && item.Model.DatabaseIndex.Equals(selectedId.Value))
                    selected = item;
            }

            if (selected != null)
            {
                InputManager.IsDragSelecting = true;
                SelectedItem = selected;
                InputManager.IsDragSelecting = false;
            }
        }
        ScrollToOffset?.Invoke(offset);
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
    public int GetItemIndex(PlaylistViewModel album)=>Items?.IndexOf(album)??-1;

    public override NavCapsuleViewModel? GetCapsule()
    {
        if(SelectedItem != null)
        {
            return new NavCapsuleViewModel()
            {
                Title = SelectedItem.FileName,
                SubTitle = SelectedItem.FilePath,
                Artwork = SelectedItem.Artwork,
            };
        }
        return null;
    }

    public void RemoveItem(PlaylistViewModel item)
    {
        if(item.IsSelected)
            item.IsSelected = false;
        if (_items.Remove(item))
        {
            Sort();
        }
    }
}
