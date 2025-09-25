/*
using System;
using Mpv.NET.Player;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Models;

public class MpvAudioPlayer : IDisposable
{
    private readonly MpvPlayer _mpv;
    private readonly Action _trackEnded;

    public TrackViewModel Track { get; }

    public bool IsPlaying => _mpv.IsPlaying;

    public MpvAudioPlayer(TrackViewModel track, Action trackEnded)
    {
        Track = track;
        _trackEnded = trackEnded;

        // Create player (null => no window)
        _mpv = new MpvPlayer(null)
        {
            AutoPlay = false
        };

        _mpv.MediaFinished += (_, _) => _trackEnded.Invoke();

        // Load track
        _mpv.Load(Track.Model.FilePath);
    }

    public void Play() => _mpv.Resume();

    public void Pause() => _mpv.Pause();

    public void Restart()
    {
        _mpv.SeekAsync(0);
        _mpv.Resume();
    }

    public void Stop() => _mpv.Stop();

    public void SetVolume(double volume) => _mpv.Volume = (int)(volume * 100.0)          ;

    public float GetVolume() => (float)(_mpv.Volume / 100.0);

    public float GetPosition() => (float)(_mpv.Position / _mpv.Duration);

    public void SetPosition(float pos)
    {
        pos = Math.Clamp(pos, 0f, 1f);
        _mpv.Position = pos * _mpv.Duration;
    }

    public TimeSpan GetLength() => _mpv.Duration; // seconds

    
    public void Dispose()
    {
        if (IsPlaying) Stop();
        _mpv.Dispose();
    }
}
*/