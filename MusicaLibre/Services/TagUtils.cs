using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Channels;
using System.Threading.Tasks;
using MusicaLibre.Models;
namespace MusicaLibre.Services;
using System.IO;

public static class TagUtils
{
    public static  T? Coalesce<T>(T[] values)
    {
        if (values == null || values.Length == 0)
            return default;

        // take first value as reference
        var first = values[0];

        // use EqualityComparer<T> to handle nulls & custom equality
        var comparer = EqualityComparer<T>.Default;

        for (int i = 1; i < values.Length; i++)
        {
            if (!comparer.Equals(first, values[i]))
                return default; // mismatch -> return null/default
        }

        return first;
    }


    private static readonly Channel<Track> _channel =
        Channel.CreateUnbounded<Track>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false
        });

    private static Task? _consumerTask;
    private static readonly object _lock = new();

    public static void EnqueueFileUpdate(Track track)
    {
        // Start consumer if not running
        lock (_lock)
        {
            if (_consumerTask == null || _consumerTask.IsCompleted)
            {
                _consumerTask = Task.Run(ConsumeAsync);
            }
        }

        _channel.Writer.TryWrite(track);
    }

    private static async Task ConsumeAsync()
    {
        await foreach (var track in _channel.Reader.ReadAllAsync())
        {
            if (!File.Exists(track.FilePath)) return;
            try
            {
                var created = File.GetCreationTime(track.FilePath);
                var modified = File.GetLastWriteTime(track.FilePath);
                using var file = TagLib.File.Create(track.FilePath);
                file.Tag.Title = track.Title;
                file.Tag.Album = track.Album?.Title;
                file.Tag.AlbumArtists = [track.Album?.AlbumArtist.Name ?? ""];
                file.Tag.Genres = track.Genres.Select(x => x.Name).ToArray();
                file.Tag.Year = track.Year?.Number ?? 0;
                file.Tag.Composers = track.Composers.Select(x => x.Name).ToArray();
                file.Tag.RemixedBy = track.Remixer?.Name;
                file.Tag.Performers = track.Artists.Select(x => x.Name).ToArray();
                file.Tag.Conductor = track.Conductor?.Name;
                file.Tag.Disc = track.DiscNumber;
                file.Tag.Track = track.TrackNumber;
                file.Tag.Comment = track.Comment;
                file.Save();
                File.SetCreationTime(track.FilePath, created);
                File.SetLastWriteTime(track.FilePath, modified);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tag update failed for {track.FilePath}: {ex}");
            }
        }
    }

    // Optional: graceful shutdown method
    public static async Task CompleteAsync()
    {
        _channel.Writer.Complete();
        if (_consumerTask != null)
            await _consumerTask;
    }
}