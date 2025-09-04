using System;
using LibVLCSharp.Shared;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Models;

public class VlcAudioPlayer
{
    public TrackViewModel Track { get; set; }
    private MediaPlayer _mediaPlayer;
    private readonly Action _trackEnded;
    private LibVLC _libVLC;

    public bool IsPlaying => _mediaPlayer.IsPlaying;
    public VlcAudioPlayer(LibVLC lib, TrackViewModel track, Action trackEnded)
    {
        _libVLC = lib;
        Track = track;
        _trackEnded = trackEnded;
        var media = new Media(_libVLC, track.Model.FilePath!, FromType.FromPath);
        _mediaPlayer = new MediaPlayer(media);

        _mediaPlayer.EndReached += (_, _) => _trackEnded.Invoke();
    }

    public void Play()
    {
        if(!_mediaPlayer.IsPlaying) _mediaPlayer.Play();
        else _mediaPlayer.SetPause(false);
    } 
    public void Pause()
    {
        if (!_mediaPlayer.IsPlaying) _mediaPlayer.Play();
        _mediaPlayer.SetPause(true);
    }

    public void Stop()=>_mediaPlayer.Stop();

    public void SetVolume(double volume) => _mediaPlayer.Volume = (int)(volume * 100);

    public float GetVolume() => _mediaPlayer.Volume / 100f;

    // Position is 0..1
    public float GetPosition() => _mediaPlayer.Position;

    public void SetPosition(float pos)=>_mediaPlayer.Position = Math.Clamp(pos, 0f, 1f);
    public double GetLength() => _mediaPlayer.Length / 1000.0; // seconds

    public void Dispose()
    {
        if(IsPlaying)Stop();
        _mediaPlayer.Dispose();
    }
}