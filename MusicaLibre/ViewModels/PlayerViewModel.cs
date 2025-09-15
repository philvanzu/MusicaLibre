using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using MusicaLibre.Models;
using MusicaLibre.Services;
namespace MusicaLibre.ViewModels;

public partial class PlayerViewModel : ViewModelBase
{
    private readonly LibVLC _libVLC;
    
    public VlcAudioPlayer? CurrentPlayer { get; set; }
    public VlcAudioPlayer? NextPlayer { get; set; }
    
    [ObservableProperty] private bool _isSeeking;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private double _position;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private string _elapsed;
    [ObservableProperty] private string _remaining;
    [ObservableProperty] private string _duration;

    [ObservableProperty] private double _volume=1;
    NowPlayingListViewModel? _nowPlayingList;


    private DispatcherTimer _positionTimer;

    [ObservableProperty] private MainWindowViewModel _mainVM;
    public TrackViewModel? CurrentTrack => _nowPlayingList?.PlayingTrack;
    public TrackViewModel? NextTrack => _nowPlayingList?.NextTrack;

    public PlayerViewModel(MainWindowViewModel mainVm)
    {
        Core.Initialize(); // Load libvlc
        _libVLC = new LibVLC();
        MainVM = mainVm;
        MainVM.PropertyChanged += OnMainVMPropertyChanged;
        
        
        _positionTimer = new DispatcherTimer();
        _positionTimer.Interval = TimeSpan.FromMilliseconds(150);
        _positionTimer.Tick += OnPositionTimerTick;
        
    }

    private void OnMainVMPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainVM.Library))
        {
            if (MainVM.Library != null)
            {
                _nowPlayingList = MainVM.Library.NowPlayingList;
                _nowPlayingList.PropertyChanged += OnNowPlayingListPropertyChanged;
            }
            else if(_nowPlayingList != null)
            {
                _nowPlayingList.PropertyChanged -= OnNowPlayingListPropertyChanged;
                _nowPlayingList = null;
            }
        }
    }
    private void OnNowPlayingListPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
            if (e.PropertyName == nameof(_nowPlayingList.PlayingTrack)) LoadCurrentTrack();
            else if (e.PropertyName == nameof(_nowPlayingList.NextTrack)) PreloadNextTrack();
    }

    public void OnPositionTimerTick(object? sender, EventArgs eventArgs)
    {
        if (CurrentPlayer != null && CurrentTrack != null)
        {
            var filepos = CurrentPlayer.GetPosition();
            var trackpos = CurrentTrack.FileToTrackPosition(filepos);
            var elapsed = trackpos * CurrentTrack.TrackDuration.Value;
            Elapsed = TimeUtils.FormatDuration(elapsed);
            var remaining = CurrentTrack.TrackDuration.Value - elapsed;
            Remaining = TimeUtils.FormatDuration(remaining);
            if (!IsSeeking)
                Position = trackpos;
            
            if(trackpos >= 1 && CurrentTrack.FileIsMultitrack && !CurrentTrack.IsLastMultitrackFileTrack) 
                TrackEnded();
        }
        if (CurrentPlayer == null)
            Position = 0;

        
    }
    partial void OnVolumeChanged(double value)
    {
        if (CurrentPlayer != null)
            CurrentPlayer.SetVolume(value);
    }
    partial void OnPositionChanged(double value)
    {
        if (!IsLoaded)
            Position = 0;
    }

    partial void OnIsPlayingChanged(bool value)
    {
        if(value) _positionTimer.Start();
        else _positionTimer.Stop();
    }
    public void SetTrackPosition(double trackPosition)
    {
        Position = trackPosition;
        if (CurrentPlayer != null)
        {
            var filepos = CurrentTrack.TrackToFilePosition(trackPosition);
            CurrentPlayer.SetPosition( (float)filepos );
        } 
    }

    public void LoadCurrentTrack()
    {
        // check if scheduled track is already playing
        if (CurrentPlayer?.Track != CurrentTrack || CurrentTrack == null)  
        {
            //cleanup if any leftover track in the play queue
            if (CurrentPlayer != null)
            {
                if(CurrentPlayer.IsPlaying)Stop();
                ReleasePlayer(CurrentPlayer);    
            }
            if (CurrentTrack != null)
            {
                CurrentPlayer = new VlcAudioPlayer(_libVLC, CurrentTrack, TrackEnded);
                CurrentPlayer.SetVolume(Volume);

                IsLoaded = true;
                CurrentPlayer.Play();
                if(CurrentTrack.FileIsMultitrack)
                    CurrentPlayer.SetPosition( (float)CurrentTrack.TrackToFilePosition(0) );
                IsPlaying = true;
            }
            else
            {
                IsLoaded = false;
                CurrentPlayer = null;
            }
        }
        Duration = (CurrentTrack != null)? TimeUtils.FormatDuration(CurrentTrack.TrackDuration.Value): string.Empty;
    }

    public void PreloadNextTrack()
    {
        if (NextPlayer?.Track != NextTrack ||  NextTrack == null)
        {
            if (NextPlayer != null)
            {
                if(NextPlayer.IsPlaying)NextPlayer.Stop();
                ReleasePlayer(NextPlayer);
            }
            
            if (NextTrack != null)
            {
                NextPlayer=new VlcAudioPlayer(_libVLC, NextTrack,  TrackEnded);

                NextPlayer.Pause();
                if(NextTrack.FileIsMultitrack)
                    NextPlayer.SetPosition((float) NextTrack.TrackToFilePosition(0));
            }
            else NextPlayer = null;
        }
    }
    
    private int _endedFlag = 0;
    public void TrackEnded()
    {
        if (Interlocked.Exchange(ref _endedFlag, 1) == 1)
            return; // already handled
        try{
            if (CurrentPlayer != null)
            {
                ReleasePlayer(CurrentPlayer);
                CurrentPlayer = null;  
            }
                
            if (NextPlayer != null)
            {
                CurrentPlayer = NextPlayer;
                Duration = TimeUtils.FormatDuration(CurrentTrack.TrackDuration.Value);
                CurrentPlayer.SetVolume(Volume);
                CurrentPlayer.Play();
                NextPlayer = null;
            }
            else
            {
                IsPlaying = false;
                IsLoaded = false;
            }
            _nowPlayingList?.Next();
        }
        finally
        {
            // reset flag for next track cycle
            Interlocked.Exchange(ref _endedFlag, 0);
        }
    }

    public void ReleasePlayer(VlcAudioPlayer player)
    {
        Dispatcher.UIThread.Post(player.Dispose);
    }

    

    [RelayCommand] void PlayToggle()
    {
        if (IsLoaded)
        {
            if (CurrentPlayer != null && CurrentPlayer.IsPlaying)
            {
                CurrentPlayer?.Pause();
                IsPlaying = false;
            }
            else if (CurrentPlayer != null)
            {
                bool stopped = !CurrentPlayer.IsPlaying;
                CurrentPlayer?.Play();
                if (stopped && CurrentTrack.FileIsMultitrack)
                    CurrentPlayer.SetPosition( (float)CurrentTrack.TrackToFilePosition(0) );
                IsPlaying = true;
            }
        }
        else LoadCurrentTrack();
    }

    [RelayCommand] void Stop()
    {
        SetTrackPosition(0);
        CurrentPlayer?.Stop();
        IsPlaying = false;
        Position = 0;
    }

    [RelayCommand] void SkipForward()
    {
        _nowPlayingList?.Next();
    }

    [RelayCommand] void SkipBack()
    {
        _nowPlayingList?.Previous();
    }

    [RelayCommand] void Rewind()
    {
        Stop();
        PlayToggle();
    }

    [RelayCommand]
    void Shuffle()
    {
        
    }

    [RelayCommand] void Repeat(){}

}