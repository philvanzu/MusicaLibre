using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.Views;

namespace MusicaLibre.ViewModels;
public abstract partial class NameTagsPresenterViewModelBase : LibraryDataPresenter, ISelectVirtualizableItems
{
    
    protected ObservableCollection<NameTagViewModelBase> _itemsMutable = new();
    public ReadOnlyObservableCollection<NameTagViewModelBase>? Items { get; set; }
    [ObservableProperty] protected NameTagViewModelBase? _selectedItem;

    partial void OnSelectedItemChanged(NameTagViewModelBase? value)=>SelectedItemChanged(value);
    protected abstract void SelectedItemChanged(NameTagViewModelBase? value);
    
    public Action<double>? ScrollToOffset;
    public double ScrollOffset {get; set;}

    // maybe put common properties like `Items` and `SelectedItem` here
    public event EventHandler<SelectedItemChangedEventArgs>? SelectionChanged;
    public event EventHandler? SortOrderChanged;
    public event EventHandler<int>? ScrollToIndexRequested;

    public NameTagsPresenterViewModelBase(LibraryViewModel library, List<Track> tracksPool)
        :base(library, tracksPool)
    {
        Items = new ReadOnlyObservableCollection<NameTagViewModelBase>(_itemsMutable);
    }
    public int GetIndex(NameTagViewModelBase item)=> Items?.IndexOf(item) ?? -1;
    public int GetSelectedIndex() => SelectedItem != null ? GetIndex(SelectedItem) : -1;
}
public partial class NameTagsListViewModel<T> : NameTagsPresenterViewModelBase, ISelectVirtualizableItems where T : NameTag
{
    
    protected List<NameTagViewModel<T>> _items = new();

    protected override void SelectedItemChanged(NameTagViewModelBase? value)
    {
        if (!InputManager.CtrlPressed)
        {
            foreach (var album in _items)
            {
                if(album != value && album.IsSelected)
                    album.IsSelected = false;
            }
        } 
        
        OnPropertyChanged(nameof(SelectedItemTracks));
        SelectedItems =  _items.Where(x => x.IsSelected).ToList();
        
        var selectedTracks = new List<Track>();
        foreach(var item in SelectedItems)
            selectedTracks.AddRange(item.Tracks);
        
        SelectedTracks = selectedTracks;
        
        
           
    }

    [ObservableProperty] private List<NameTagViewModel<T>>? _selectedItems;

    public List<Track>? SelectedItemTracks => SelectedItem?.Tracks;
    
    Func<List<Track>, T, List<Track>> ItemTracksDelegate {get;set;} 
    Func<List<Track>, List<T>> ItemsDelegate {get;set;}

    
    public NameTagsListViewModel(LibraryViewModel library, List<Track> tracksPool, 
        Func<List<Track>, T, List<Track>> itemTracksDelegate, 
        Func<List<Track>, List<T>> itemsDelegate) 
        :base(library, tracksPool)
    {
        ItemTracksDelegate = itemTracksDelegate;
        ItemsDelegate = itemsDelegate;
        UpdateCollection();
        _initialized = true;
    }

    public void UpdateCollection( )
    {
        _items.Clear();

        var models =ItemsDelegate.Invoke(TracksPool);
        _items = models.Select(x => new NameTagViewModel<T>(x, this, ItemTracksDelegate)).ToList();
        Sort();
    }


    private IComparer<NameTagViewModel<T>> GetComparer(NameSortKeys sort, bool ascending)
    {
        return sort switch
        {
            NameSortKeys.Name => ascending
                ? SortExpressionComparer<NameTagViewModel<T>>.Ascending(x => x.Title)
                : SortExpressionComparer<NameTagViewModel<T>>.Descending(x => x.Title),
            NameSortKeys.Random => 
                SortExpressionComparer<NameTagViewModel<T>>.Ascending(x => x.RandomIndex),

            _ => SortExpressionComparer<NameTagViewModel<T>>.Ascending(x => x.DatabaseIndex)
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
        
        var groupWeights = new Dictionary<NameTagViewModel<T>, double>();
        foreach (var item in _items)
        {
            double w = 0;
            foreach(var track in item.Tracks)
                w += weights.GetValueOrDefault(track);
            if(w > 0)
                groupWeights.Add(item, w);
        }
        var filtered = _items.Where(x => groupWeights.GetValueOrDefault(x) > 0);
        var ordered = filtered.OrderByDescending(x => groupWeights.GetValueOrDefault(x));
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

        if(Library.CurrentStep.SortingKeys.Any(x => x is SortingKey<NameSortKeys> sk && sk.Key == NameSortKeys.Random))
            ShufflePages();
        
        var comparers = Library.CurrentStep.SortingKeys
            .Select(key => GetComparer((key as SortingKey<NameSortKeys>).Key, key.Asc))
            .ToList();

        Ascending = Library.CurrentStep.SortingKeys.First().Asc;
        var sorted = _items.OrderBy(x => x, new CompositeComparer<NameTagViewModel<T>>(comparers));

        _itemsMutable.Clear();
        _itemsMutable.AddRange(sorted);

        OnPropertyChanged(nameof(Items));
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
        NameTagViewModelBase? selected = null;
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
    public override NavCapsuleViewModel? GetCapsule()
    {
        if(SelectedItem != null)
        {
            return new NavCapsuleViewModel()
            {
                Title = SelectedItem.Title,
                SubTitle = SelectedItem.Count,
                Artwork = SelectedItem.Artwork,
            };
        }
        return null;
    }
}

public class ArtistsListViewModel(LibraryViewModel library, List<Track> tracksPool) :
    NameTagsListViewModel<Artist>(library, tracksPool,
        (t, a) => t.Where(x => x.Artists.Contains(a)).ToList(),
        t =>
        {
            var ids =  t.SelectMany(track => track.Artists).Select(artist => artist.DatabaseIndex)
                .Distinct().ToList();
            return library.Data.Artists.Values.Where(x => ids.Contains(x.DatabaseIndex)).ToList();
        });

public class PublishersListViewModel(LibraryViewModel library, List<Track> tracksPool): 
    NameTagsListViewModel<Publisher>(library, tracksPool,
        (t, p) => t.Where(x => x.Publisher == p).ToList(),
        t =>
        {
            var ids = t.Select(track => track.PublisherId).Where(id => id.HasValue).Distinct().ToList();
            return library.Data.Publishers.Values.Where(x => ids.Contains(x.DatabaseIndex)).ToList();
        });

public class GenresListViewModel(LibraryViewModel library, List<Track> tracksPool):
    NameTagsListViewModel<Genre>(library, tracksPool,
        (t, g) => t.Where(x => x.Genres.Contains(g)).ToList(),
        t =>
        {
            var ids =  t.SelectMany(track => track.Genres).Select(genre => genre.DatabaseIndex)
                .Distinct().ToList();
            return library.Data.Genres.Values.Where(x => ids.Contains(x.DatabaseIndex)).ToList();
        });

public class RemixersListViewModel(LibraryViewModel library, List<Track> tracksPool) :
    NameTagsListViewModel<Artist>(library, tracksPool,
        (t, a) => t.Where(x => x.Remixer == a).ToList(),
        t =>
        {
            var ids =  t.Select(track => track.RemixerId).Where(id => id.HasValue).Distinct().ToList();
            return library.Data.Artists.Values.Where(x => ids.Contains(x.DatabaseIndex)).ToList();
        });

public class ConductorsListViewModel(LibraryViewModel library, List<Track> tracksPool) :
    NameTagsListViewModel<Artist>(library, tracksPool,
        (t, a) => t.Where(x => x.Conductor == a).ToList(),
        t =>
        {
            var ids = t.Select(track => track.ConductorId).Where(id => id.HasValue).Distinct().ToList();
            return library.Data.Artists.Values.Where(x => ids.Contains(x.DatabaseIndex)).ToList();
        });

public class ComposersListViewModel(LibraryViewModel library, List<Track> tracksPool) :
    NameTagsListViewModel<Artist>(library, tracksPool,
        (t, a) => t.Where(x => x.Composers.Contains(a)).ToList(),
        t =>
        {
            var ids = t.SelectMany(track => track.Composers)
                .Select(composer => composer.DatabaseIndex)
                .Distinct().ToList();
            return library.Data.Artists.Values.Where(x => ids.Contains(x.DatabaseIndex)).ToList();
        });

public class AudioFormatsListViewModel(LibraryViewModel library, List<Track> tracksPool) :
    NameTagsListViewModel<AudioFormat>(library, tracksPool,
        (t, a) => t.Where(x => x.AudioFormat == a).ToList(),
        t =>
        {
            var ids = t.Select(track => track.AudioFormatId).Distinct().ToList();
            return library.Data.AudioFormats.Values.Where(x => ids.Contains(x.DatabaseIndex)).ToList();
        });

public class YearsListViewModel(LibraryViewModel library, List<Track> tracksPool) :
    NameTagsListViewModel<Year>(library, tracksPool,
        (t, a) => t.Where(x => x.Year == a).ToList(),
        t =>
        {
            var ids = t.Select(track => track.YearId).Distinct().ToList();
            return  library.Data.Years.Values.Where(x => ids.Contains(x.DatabaseIndex)).ToList();
        });

public class FoldersListViewModel(LibraryViewModel library, List<Track> tracksPool) :
    NameTagsListViewModel<Folder>(library, tracksPool,
        (t, a) => t.Where(x => x.Folder == a).ToList(),
        t =>
        {
          var ids = t.Select(track => track.FolderId).Distinct().ToList();
          return  library.Data.Folders.Values.Where(x => ids.Contains(x.DatabaseIndex)).ToList();
        } );



