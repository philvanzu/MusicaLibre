using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using MusicaLibre.Models;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public partial class NowPlayingListViewModel:TracksListViewModel
{
    private int _playingTrackIndex=-1;
    [ObservableProperty] private TrackViewModel? _playingTrack;
    [ObservableProperty] private TrackViewModel? _nextTrack;
    [ObservableProperty] private string? _countInfo;
    [ObservableProperty] private string? _durationInfo;
    [ObservableProperty] private int _repeatStateIdx;
    [ObservableProperty] private int _shuffleStateIdx;

    
    private bool _shuffled;

    private TimeSpan _totalDuration = TimeSpan.Zero;
    private TimeSpan _elapsed = TimeSpan.Zero;
    private uint _count = 0;
    public NowPlayingListViewModel(LibraryViewModel library, List<Track> tracksPool) : base(library, tracksPool) { }

    public IEnumerable<TrackViewModel> SortTracks (IEnumerable<TrackViewModel> tracks)
    {
        var step = Library.CurrentStep;
        var sks = step.Type == OrderGroupingType.Track? step.SortingKeys.Cast<SortingKey<TrackSortKeys>>() :step.TracksSortingKeys;
        if (_shuffled)
        {
            foreach (var track in tracks)
                track.RandomIndex = CryptoRandom.NextInt();
            sks = new[] { new SortingKey<TrackSortKeys>(TrackSortKeys.Random) };
        }
        var comparers = sks.Select(x => TracksListViewModel.GetComparer(x.Key, x.Asc));
        return tracks.OrderBy(x => x, new CompositeComparer<TrackViewModel>(comparers));
    }
    public void Replace(List<Track> replace)
    {
        if (replace == null || replace.Count == 0) return;
        var vms = replace.Select(x => new TrackViewModel(x, this));
        _items = SortTracks(vms).ToList();
        Update();
        Items.First().IsPlaying = true;    
    }
    public void Insert(List<Track> insert, int position=-1)
    {
        if (insert == null || insert.Count == 0) return;
        var vms = insert.Select(x => new TrackViewModel(x, this)).ToList();
        vms = SortTracks(vms).ToList();
        if (position == -1) position = _playingTrackIndex+1;
        _items.InsertRange(position, vms);
        Update();
    }

    public void Append(List<Track> append)
    {
        if(append == null || append.Count == 0) return;
        var vms = append.Select(x => new TrackViewModel(x, this)).ToList();
        vms = SortTracks(vms).ToList();
        _items.AddRange(vms);
        Update();
    }

    public void Update()
    {
        int i = 0;
        _totalDuration = TimeSpan.Zero;
        foreach (var track in _items)
        {
            track.NowPlayingIndex = ++i;
            _totalDuration += track.Model.Duration;
        }

        _count = (uint) i;
        _itemsMutable.Clear();
        _itemsMutable.AddRange(_items);
        
        AppData.Instance.AppState.NowPlayingTrackIds.Clear();
        AppData.Instance.AppState.NowPlayingTrackIds.AddRange(
            Items.Select(x=> x.Model.DatabaseIndex));
    }
    partial void OnPlayingTrackChanged(TrackViewModel? value)
    {
        if (PlayingTrack == null)
        {
            Console.WriteLine("PlayingTrack set to null");
            AppData.Instance.AppState.NowPlayingTrackId = null;
        }
        else 
            AppData.Instance.AppState.NowPlayingTrackId = PlayingTrack.Model.DatabaseIndex;
            

        var pos = value is not null? Items.IndexOf(value):-1;
        _elapsed = TimeSpan.Zero;
        int i = 0;
        foreach (var item in _items)
        {
            i++;
            if(pos > -1 && i <= pos)
                _elapsed += item.Model.Duration;
            if(item.IsPlaying && item != value)
                item.IsPlaying = false;
        }
        if (value != null)
        {
            _playingTrackIndex = GetItemIndex(value);
            var next = _playingTrackIndex + 1;
            if (next < _items.Count) NextTrack = _items[next];   
            else NextTrack = null;
        }
        else
        {
            NextTrack = null;
            _playingTrackIndex = -1;
        }
        CountInfo = value is not null ?  $"Now Playing track {pos+1}/{_count}": string.Empty;
        DurationInfo = $"{_elapsed:hh\\:mm\\:ss} / {_totalDuration:hh\\:mm\\:ss}";
        if(pos > -1)
            ScrollToIndex(pos);
    }

    public void OnPlayerTimerTick(TimeSpan trackElapsed)
    {
        var elapsed = _elapsed + trackElapsed;
        DurationInfo = $"{elapsed:hh\\:mm\\:ss} / {_totalDuration:hh\\:mm\\:ss}";
    }
    public void SetIsPlaying(int index)
    {
        if (index == _playingTrackIndex) return;
        if (index < 0 || index >= _items.Count) return;
        Items[index].IsPlaying = true;
    }
    public void Next()
    {
        var pos = _playingTrackIndex + 1;
        if( pos < _items.Count) SetIsPlaying(pos);
        else
        {
            if(RepeatStateIdx == 2) SetIsPlaying(0);
            else if (PlayingTrack is not null) 
                PlayingTrack.IsPlaying = false;
        }
    }

    public void Previous()
    {
        var pos = _playingTrackIndex - 1;
        if (pos >= 0) SetIsPlaying(pos);
    }

    partial void OnShuffleStateIdxChanged(int value)
    {
        if(value == 0  && _shuffled) // Shuffle:Off
        {
            _shuffled = false;
            _items = SortTracks(_items).ToList();
            Update();
        }
        else if (value == 1 && ! _shuffled) // Shuffle:On
        {
            _shuffled = true;
            _items = SortTracks(_items).ToList();
            Update();
        }
    }

    [RelayCommand] async Task SavePlaylist()
    {
        if(Items.Count == 0) return;
        
        var playlistsPath = Library.Settings.UserPlaylistsPath;
        if (!Path.IsPathRooted(playlistsPath))
            playlistsPath = Path.Combine(Library.Path, playlistsPath);
        
        if(!Directory.Exists(playlistsPath))
            Directory.CreateDirectory(playlistsPath);
        
        var suggestedName = "Mixtape.m3u";
        var i = 0;
        while (File.Exists(Path.Combine(playlistsPath, suggestedName)))
            suggestedName = $"Mixtape({++i}).m3u";
            
        var filters = new[]
        {
            new FilePickerFileType("M3U Playlist")
            {
                Patterns = new[] { "*.m3u", "*.m3u8" },
                MimeTypes = new[] { "audio/x-mpegurl", "application/vnd.apple.mpegurl" }
            }
        };
        var outputPath = await DialogUtils.SaveFileAsync(Library.MainWindowViewModel.MainWindow, playlistsPath, filters, suggestedName);
        if (outputPath == null) return;
        if (File.Exists(outputPath))
        {
            if (! await DialogUtils.YesNoDialog(Library.MainWindowViewModel.MainWindow,
                    "File already exists!",
                    $"{outputPath} already exists! Overwrite?"))
                return;
        }
            
        Playlist.CreateM3u(outputPath, Items.Select(x=> x.Model.FilePath).ToList());
    }
    [RelayCommand] void RemoveSelection()
    {
        if (SelectedItem == null) return;
        foreach (var item in SelectedItems)
            _items.Remove(item);
        Update();
    }

    [RelayCommand] void MoveSelectionUp()
    {
        var firstIndex = _items.IndexOf(SelectedItems.First());
        var lastIndex = _items.IndexOf(SelectedItems.Last());
        if (firstIndex <= 0)
            return; // already at top, nothing to do

        // Grab the item just before the block
        var itemAbove = _items[firstIndex - 1];

        // Remove the block
        var block = _items.Skip(firstIndex).Take(lastIndex - firstIndex + 1).ToList();
        for (int i = 0; i < block.Count; i++)
            _items.RemoveAt(firstIndex);

        // Reinsert before the "itemAbove"
        int insertIndex = firstIndex - 1;
        foreach (var track in block)
            _items.Insert(insertIndex++, track);
        
        Update();
    }

    [RelayCommand] void MoveSelectionDown()
    {
        var firstIndex = _items.IndexOf(SelectedItems.First());
        var lastIndex = _items.IndexOf(SelectedItems.Last());
        if (lastIndex >= _items.Count - 1)
            return; // already at bottom, nothing to do

        // Grab the item just after the block
        var itemBelow = _items[lastIndex + 1];

        // Remove the block
        var block = _items.Skip(firstIndex).Take(lastIndex - firstIndex + 1).ToList();
        for (int i = 0; i < block.Count; i++)
            _items.RemoveAt(firstIndex);

        // Reinsert after the "itemBelow"
        int insertIndex = firstIndex + 1;
        foreach (var track in block)
            _items.Insert(insertIndex++, track);
        
        Update();
    }
    
}