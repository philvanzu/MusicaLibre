using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using MusicaLibre.Models;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public partial class TracksListViewModel:LibraryDataPresenter, ISelectVirtualizableItems
{
    protected List<TrackViewModel> _items = new();
    protected ObservableCollection<TrackViewModel> _itemsMutable = new();
    public ReadOnlyObservableCollection<TrackViewModel> Items { get; init; }

    TrackViewModel? _shiftSelectionAnchor;
    [ObservableProperty] protected List<TrackViewModel> _selectedItems;
    [ObservableProperty] protected TrackViewModel? _selectedItem;
    [ObservableProperty] private bool _isContiguousSelection;
    partial void OnSelectedItemChanged(TrackViewModel? value)
    {
        if (!InputManager.CtrlPressed && !InputManager.IsDragSelecting && !InputManager.ShiftPressed)
        {
            foreach (var item in _items)
            {
                item.IsSelected = item == value;
            }    
        }

        if (InputManager.ShiftPressed && _shiftSelectionAnchor != null && value != null)
        {
            var startIdx = GetItemIndex(_shiftSelectionAnchor);
            var endIdx = GetItemIndex(value);
            var step = startIdx <= endIdx ? 1 : -1;
            for (var i = startIdx; i != endIdx + step; i += step)
                Items[i].IsSelected = true;
        }
        
        if(!InputManager.ShiftPressed) 
            _shiftSelectionAnchor = value;
        
        SelectedItems = Items.Where(x => x.IsSelected).ToList();
        SelectedTracks = SelectedItems.Select(x=>x.Model).ToList();
        
        var selectedIndices = new List<int>();
        foreach (var item in SelectedItems)
            selectedIndices.Add(Items.IndexOf(item));
        var sorted = selectedIndices.OrderBy(x=>x).ToList();
        bool isContiguousSelection = true;
        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i] != sorted[i - 1] + 1)
            {
                isContiguousSelection = false;
                break;
            }
        }
        IsContiguousSelection =  isContiguousSelection;
        SelectedTrackChanged();
    }
    protected virtual void SelectedTrackChanged(){}
    
    [ObservableProperty]protected List<TrackViewColumn> _columns;
    [ObservableProperty] protected TrackViewColumn? _sortingColumn;
    partial void OnSortingColumnChanged(TrackViewColumn? oldValue, TrackViewColumn? newValue)
    {
        if(oldValue != null) oldValue.IsSorting = false;
        if (newValue != null)
        {
            var comparer = GetComparer(newValue.SortKey, newValue.IsAscending);
            Sort(comparer);
        }
    }

    public event EventHandler<SelectedItemChangedEventArgs>? SelectionChanged;
    public event EventHandler? SortOrderChanged;
    public event EventHandler<int>? ScrollToIndexRequested;
    public Action<double>? ScrollToOffset;
    public double ScrollOffset {get; set;}
    
    public TracksListViewModel(LibraryViewModel library, List<Track> tracksPool, List<TrackViewColumn>? columns=null) : base(library, tracksPool)
    {
        Items = new ReadOnlyObservableCollection<TrackViewModel>(_itemsMutable);
        
        _columns = columns != null ? columns : new List<TrackViewColumn>() 
        {
            new("Disc",      TrackSortKeys.DiscNumber,  t => t.Model.DiscNumber.ToString()??"", this, true, true),
            new("Track",     TrackSortKeys.TrackNumber, t => t.Model.TrackNumber.ToString() ?? "", this, true, true),
            new("Title",     TrackSortKeys.Title,       t => t.Title ?? "", this),
            new("Album",     TrackSortKeys.Album,       t => t.Album ?? "", this),
            new("Artists",   TrackSortKeys.Artists,     t => t.Artists ?? "", this),
            new("Year",      TrackSortKeys.Year,        t => t.Year ?? "", this, true, true),
            new("Duration",  TrackSortKeys.Duration,    t => t.Duration ?? "", this, true, true),
            new("Genre",     TrackSortKeys.Genre,       t => t.Genres ?? "", this),
            new("Publisher", TrackSortKeys.Publisher,   t => t.Publisher ?? "", this),
            new("Added",     TrackSortKeys.Added,       t => t.Added.ToString(), this),
            new("Created",   TrackSortKeys.Created,     t => t.Created.ToString(), this),
            new("Modified",  TrackSortKeys.Modified,    t => t.Modified.ToString(), this),
            new("Played",    TrackSortKeys.Played,      t => t.Played.ToString(), this),
            new("Comment",   TrackSortKeys.Comment,     t => t.Model.Comment ?? "", this),
            new("Remixer",   TrackSortKeys.Remixer,     t => t.Remixer ?? "", this, false),
            new("Composer",  TrackSortKeys.Composer,    t => t.ComposersText ?? "", this, false),
            new("Conductor", TrackSortKeys.Conductor,   t => t.Conductor ?? "", this, false),
            new("Path",      TrackSortKeys.FilePath,    t => t.Model.FilePath ?? "", this, false),
            new("Format",    TrackSortKeys.Codec,       t => t.Model.AudioFormat?.Name ?? "", this, false),
            new("Bitrate",   TrackSortKeys.Bitrate,     t => t.Bitrate.ToString(), this, false),
            new("MimeType",  TrackSortKeys.MimeType,    t => t.Model.FileExtension ?? "", this, false),
            new("SampleRate",TrackSortKeys.SampleRate,  t => t.Model.SampleRate.ToString()??"", this, false),
            new("Channels",  TrackSortKeys.Channels,    t => t.Model.Channels.ToString()??"", this, false),
        };
    }

    public void UpdateCollection()
    {
        _items = TracksPool.Select(x => new TrackViewModel(x, this)).ToList();
        Sort();
        _initialized = true;
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
        TrackViewModel? selected = null;
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
    
    public static IComparer<TrackViewModel> GetComparer(TrackSortKeys sort, bool ascending)
    {
        return sort switch
        {
            TrackSortKeys.Title => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Title??"")
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Title??""),
            TrackSortKeys.Album => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Album??"")
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Album??""),
            TrackSortKeys.Artists=>ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Artists)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Artists),
            TrackSortKeys.Year => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.Year?.Number??0)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.Year?.Number??0),
            TrackSortKeys.FilePath => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.FilePath??"")
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.FilePath??""),
            TrackSortKeys.Folder => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.Folder?.Name??"")
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.Folder?.Name??""),
            TrackSortKeys.TrackNumber=>ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.TrackNumber)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.TrackNumber),
            TrackSortKeys.DiscNumber=>ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.DiscNumber)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.DiscNumber),
            TrackSortKeys.Remixer => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Remixer??"")
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Remixer??""),
            TrackSortKeys.Composer => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.ComposersText??"")
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.ComposersText??""),
            TrackSortKeys.Conductor => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Conductor??"")
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Conductor??""),
            TrackSortKeys.Genre => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Genres??"")
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Genres??""),
            TrackSortKeys.Publisher => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Publisher??"")
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Publisher??""),
            TrackSortKeys.Added => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.DateAdded)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.DateAdded),
            TrackSortKeys.Modified => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.Modified)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.Modified),
            TrackSortKeys.Created => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.Created)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.Created),
            TrackSortKeys.Played => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.LastPlayed??DateTime.MinValue)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.LastPlayed??DateTime.MinValue),
            TrackSortKeys.Comment => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.Comment??"")
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.Comment??""),
            TrackSortKeys.Duration => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.TrackDuration??TimeSpan.MinValue)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.TrackDuration??TimeSpan.MinValue),
            TrackSortKeys.Bitrate => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.BitrateKbps)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.BitrateKbps),
            TrackSortKeys.MimeType => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.FileExtension)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.FileExtension),
            TrackSortKeys.Codec => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.Codec)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.Codec),
            TrackSortKeys.SampleRate => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.SampleRate)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.SampleRate),
            TrackSortKeys.Channels => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.Channels)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.Model.Channels),
            TrackSortKeys.PlaylistPosition => ascending
                ? SortExpressionComparer<TrackViewModel>.Ascending(x => x.PlaylistPosition)
                : SortExpressionComparer<TrackViewModel>.Descending(x => x.PlaylistPosition),
            TrackSortKeys.Random => 
                SortExpressionComparer<TrackViewModel>.Ascending(x => x.RandomIndex),
            _ => SortExpressionComparer<TrackViewModel>.Ascending(x => x.Model.DatabaseIndex)
        };
    }

    public void ShufflePages()
    {
        foreach (var track in _items)
            track.RandomIndex = CryptoRandom.NextInt();
    }

    public override void Filter(string searchString)
    {
        if (string.IsNullOrWhiteSpace(searchString))
        {
            Sort();
            return;
        }

        var weights = SearchUtils.FilterTracks( searchString, _items.Select(x => x.Model).ToList(), Library);

        var ordered = _items.Where(x => weights.GetValueOrDefault(x.Model) > 0)
            .OrderByDescending(x => weights.GetValueOrDefault(x.Model))
            .ThenBy(x => x.Model.Title);
            
        
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
        if(Library.CurrentStep.SortingKeys.Select(x=>  (x is SortingKey<TrackSortKeys> sk) && sk.Key == TrackSortKeys.Random).Any())
            ShufflePages();

        var skeys = Library.CurrentStep.Type == OrderGroupingType.Track
            ? Library.CurrentStep.SortingKeys.Cast<SortingKey<TrackSortKeys>>()
            : Library.CurrentStep.TracksSortingKeys;
        var comparers = skeys
            .Select(key => GetComparer(key.Key, key.Asc))
            .ToList();

        Ascending = Library.CurrentStep.SortingKeys.First().Asc;
        var sorted = _items.OrderBy(x => x, new CompositeComparer<TrackViewModel>(comparers));

        _itemsMutable.Clear();
        _itemsMutable.AddRange(sorted);

        OnPropertyChanged(nameof(Items));
        //InvokeSortOrderChanged();
    }

    void Sort(IComparer<TrackViewModel> comparer)
    {
        var sorted = _items.OrderBy(x => x, comparer);
        _itemsMutable.Clear();
        _itemsMutable.AddRange(sorted);
        OnPropertyChanged(nameof(Items));
    }

    public override void Reverse()
    {
        var reversed =_itemsMutable.Reverse().ToList();
        _itemsMutable.Clear();
        _itemsMutable.AddRange(reversed);
        Ascending = !Ascending;
    }
    
    public int GetItemIndex(TrackViewModel track) => Items.IndexOf(track);
    public int GetSelectedIndex() => SelectedItem != null ? Items.IndexOf(SelectedItem) : -1;

    public int GetColumnIndex(TrackViewColumn column)=>Columns.IndexOf(column);
    private NavCapsuleViewModel? _capsule;
    public override NavCapsuleViewModel? GetCapsule() => _capsule;
    public void  SetCapsule(NavCapsuleViewModel? capsule)=>_capsule = capsule;

    protected void ScrollToIndex(int idx)
    {
        Dispatcher.UIThread.Post(()=>ScrollToIndexRequested?.Invoke(this, idx));
    }
}

