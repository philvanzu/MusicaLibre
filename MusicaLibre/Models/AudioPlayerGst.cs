/*
using System;
using Gst;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Models
{
    public class AudioPlayerGst : IDisposable
    {
        public TrackViewModel Track { get; set; }
        private Pipeline _pipeline;
        private Element _playbin;
        private readonly Action _trackEnded;

        public bool IsPlaying { get; private set; }

        public AudioPlayerGst(TrackViewModel track, Action trackEnded)
        {
            Application.Init(); // Initialize GStreamer
            Track = track;
            _trackEnded = trackEnded;

            _playbin = ElementFactory.Make("playbin", "player");
            _playbin["uri"] = new GLib.Value($"file://{track.Model.FilePath}");
            _pipeline = new Pipeline("audio-pipeline");
            _pipeline.Add(_playbin);

            // Bus to handle messages
            var bus = _pipeline.Bus;
            bus.AddSignalWatch();
            bus.Message += (o, args) =>
            {
                var msg = args.Message;
                switch (msg.Type)
                {
                    case MessageType.Eos:
                        _trackEnded?.Invoke();
                        break;
                    case MessageType.Error:
                        msg.ParseError(out GLib.GException err, out string debug);
                        Console.WriteLine($"GStreamer Error: {err.Message}, {debug}");
                        break;
                }
            };
        }

        public void Play()
        {
            _pipeline.SetState(State.Playing);
            IsPlaying = true;
        }

        public void Pause()
        {
            _pipeline.SetState(State.Paused);
            IsPlaying = false;
        }

        public void Stop()
        {
            _pipeline.SetState(State.Null);
            IsPlaying = false;
        }

        public void Restart()
        {
            Stop();
            Play();
        }

        public void SetVolume(double volume)
        {
            _playbin["volume"] = volume; // 0.0 to 1.0
        }

        public float GetVolume() => (float)(double)_playbin["volume"];

        public void SetPosition(float pos)
        {
            if (_pipeline.SeekSimple(Format.Time, SeekFlags.Flush | SeekFlags.KeyUnit,
                    (long)(pos * (double)Length * 1_000_000_000))) { }
        }

        public float GetPosition()
        {
            _pipeline.QueryPosition(Format.Time, out long pos);
            return Length > 0 ? (float)(pos / (double)Length) : 0f;
        }

        public double Length
        {
            get
            {
                _pipeline.QueryDuration(Format.Time, out long len);
                return len / 1_000_000_000.0; // seconds
            }
        }

        public void Dispose()
        {
            Stop();
            _pipeline.Dispose();
            _playbin.Dispose();
        }
    }
}
*/