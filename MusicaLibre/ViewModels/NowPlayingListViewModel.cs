using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using MusicaLibre.Models;

namespace MusicaLibre.ViewModels;

public partial class NowPlayingListViewModel:TracksListViewModel
{
    private int _playingTrackIndex;
    [ObservableProperty] private TrackViewModel? _playingTrack;
    [ObservableProperty] private TrackViewModel? _nextTrack;

    public NowPlayingListViewModel(LibraryViewModel library, List<Track> tracksPool) : base(library, tracksPool) { }
    public void Replace(List<Track> replace)
    {
        if (replace.Count > 0)
        {
            _tracks = replace.Select(x => new TrackViewModel(x, this)).ToList();
            Update();
            Tracks.First().IsPlaying = true;    
        }
    }
    public void Insert(List<Track> insert, int position=-1)
    {
        var vms = insert.Select(x => new TrackViewModel(x, this)).ToList();
        if (position == -1) position = _playingTrackIndex+1;
        _tracks.InsertRange(position, vms);
        Update();
    }

    public void Append(List<Track> append)
    {
        var vms = append.Select(x => new TrackViewModel(x, this)).ToList();
        _tracks.AddRange(vms);
        Update();
    }

    public void Update()
    {
        int i = 0;
        foreach (var track in _tracks)
        {
            track.NowPlayingIndex = ++i;
        }
        
        _tracksMutable.Clear();
        _tracksMutable.AddRange(_tracks);
    }
    partial void OnPlayingTrackChanged(TrackViewModel? value)
    {
        if(PlayingTrack == null)
            Console.WriteLine("PlayingTrack set to null");
        
        foreach (var item in _tracks)
        {
            if(item.IsPlaying && item != value)
                item.IsPlaying = false;
        }
        if (value != null)
        {
            _playingTrackIndex = GetItemIndex(value);
            var next = _playingTrackIndex + 1;
            if (next < _tracks.Count) NextTrack = _tracks[next];   
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
        if (index < 0 || index >= _tracks.Count) return;
        Tracks[index].IsPlaying = true;
    }
    public void Next()
    {
        var pos = _playingTrackIndex + 1;
        if( pos < _tracks.Count) SetIsPlaying(pos);
    }

    public void Previous()
    {
        var pos = _playingTrackIndex - 1;
        if (pos >= 0) SetIsPlaying(pos);
    }
    
    
    

}