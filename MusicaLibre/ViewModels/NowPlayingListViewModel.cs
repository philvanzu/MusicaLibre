using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using MusicaLibre.Models;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public partial class NowPlayingListViewModel:TracksListViewModel
{
    private int _playingTrackIndex;
    [ObservableProperty] private TrackViewModel? _playingTrack;
    [ObservableProperty] private TrackViewModel? _nextTrack;

    public NowPlayingListViewModel(LibraryViewModel library, List<Track> tracksPool) : base(library, tracksPool) { }

    public IEnumerable<TrackViewModel> SortTracks (IEnumerable<TrackViewModel> tracks)
    {
        var step = Library.CurrentStep;
        var sks = step.Type == OrderGroupingType.Track? step.SortingKeys.Cast<SortingKey<TrackSortKeys>>() :step.TracksSortingKeys;
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
        foreach (var track in _items)
        {
            track.NowPlayingIndex = ++i;
        }
        
        _itemsMutable.Clear();
        _itemsMutable.AddRange(_items);
    }
    partial void OnPlayingTrackChanged(TrackViewModel? value)
    {
        if(PlayingTrack == null)
            Console.WriteLine("PlayingTrack set to null");
        
        foreach (var item in _items)
        {
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
    }

    public void Previous()
    {
        var pos = _playingTrackIndex - 1;
        if (pos >= 0) SetIsPlaying(pos);
    }
    
    
    

}