using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MusicaLibre.Models;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Services;

public static class AudioTranscoder
{
    public enum OutputFormat { Flac, Mp3 }

    public static async Task<bool> ConvertAsync(
        Track track,
        string outputFile,
        OutputFormat format,
        ProgressViewModel? progress=null)
    {
        if (!File.Exists(track.FilePath))
            throw new FileNotFoundException("Source file not found.", track.FilePath);

        CancellationToken token = default;
        if (progress != null)
        {
            progress.CancellationTokenSource = new CancellationTokenSource();
            token = progress.CancellationTokenSource.Token;
        }
            
        // Build codec args
        string codecArgs = format switch
        {
            OutputFormat.Flac => "-c:a flac",
            OutputFormat.Mp3  => "-c:a libmp3lame -b:a 320k",
            _ => throw new ArgumentOutOfRangeException(nameof(format), "Unsupported format.")
        };

        // Build segment args
        string ssArg = null;
        string toArg = null;

        if (track.Start > 0)
        {
            var startTime = TimeSpan.FromTicks((long)(track.Start * track.Duration.Ticks));
            ssArg = $"-ss {FormatTime(startTime)}";
        }

        if (track.End < 1)
        {
            var endTime = TimeSpan.FromTicks((long)(track.End * track.Duration.Ticks));
            toArg = $"-to {FormatTime(endTime)}";
        }

        // Sampling args
        string? samplingArgs = track.SampleRate > 44100 ? "-ar 44100" : null;

        var metadataComposers = track.Composers.Count > 0 ? 
            $"-metadata composer=\"{Utils.Escape(string.Join(", ", track.Composers.Select(g => g.Name)))}\"" : null;
        var metadataGenres = track.Genres.Count > 0 ? 
            $"-metadata genre=\"{Utils.Escape(string.Join(", ", track.Genres.Select(g => g.Name)))}\"" : null;
        var metadataPerformers = track.Artists.Count > 0 ? 
            $"-metadata artist=\"{Utils.Escape(string.Join(", ", track.Artists.Select(artist => artist.Name)))}\"" : null;
        var metadataPublisher = !string.IsNullOrEmpty(track.Publisher?.Name) ? 
            $"-metadata publisher=\"{Utils.Escape(track.Publisher.Name)}\"" : null; 
        
        var metadataArgs = string.Join(" ", new[]
        {
            $"-metadata title=\"{Utils.Escape(track.Title)}\"",
            $"-metadata album=\"{Utils.Escape(track.Album.Title)}\"",
            $"-metadata album_artist=\"{Utils.Escape(track.Album.AlbumArtist.Name)}\"",
            $"-metadata date=\"{Utils.Escape(track.Year.Name)}\"",
            $"-metadata disc=\"{track.DiscNumber}\"",
            $"-metadata track=\"{track.TrackNumber}\"",
            $"-metadata comment=\"{Utils.Escape(track.Comment)}\"",
            metadataPerformers,
            metadataGenres,
            metadataComposers,
            metadataPublisher,
        });
        
        
        string streamMapArgs  = "-map 0:a -map 0:v?";
        string coverArgs      = "-c:v copy";

        // Combine args safely
        var args = string.Join(" ",
            new[]
            {
                "-hide_banner",
                "-y",
                "-i", $"\"{track.FilePath}\"",
                ssArg,
                toArg,
                samplingArgs,
                metadataArgs,
                streamMapArgs,
                codecArgs,
                coverArgs,
                $"\"{outputFile}\""
            }.Where(s => !string.IsNullOrWhiteSpace(s))
        );

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        // Cancellation registration
        token.Register(() =>
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            }
        });

        // Read error output continuously
        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;

            var match = Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+)\.(\d+)");
            if (match.Success)
            {
                var hours = int.Parse(match.Groups[1].Value);
                var minutes = int.Parse(match.Groups[2].Value);
                var seconds = int.Parse(match.Groups[3].Value);
                double fraction = double.Parse("0." + match.Groups[4].Value, CultureInfo.InvariantCulture);


                var current = new TimeSpan(0, hours, minutes, seconds) + TimeSpan.FromSeconds(fraction);
                // If segment is used, adjust denominator
                var totalSegment = (track.End - track.Start) * track.Duration.TotalSeconds;
                double progressValue = current.TotalSeconds / totalSegment;
        
                if(progress != null)
                    progress.Progress.Report(($"{track.Title} : {progressValue:F1}%", progressValue, false));
                Console.WriteLine($"Progress: {progressValue:P1}");
                // Or raise an event / callback
            }
        };
        if(progress != null)
            progress.Progress.Report(($"{track.Title} : {0:F1}%",0, false));
        process.Start();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(token);
        var success = process.ExitCode == 0;

        if(progress != null)
            progress.Progress.Report(($"{track.Title} : done", 1, false));
        if (token.IsCancellationRequested)
            return false;

        return success;
    }

    private static string FormatTime(TimeSpan ts)
    {
        // Format as HH:MM:SS.mmm for FFmpeg
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }
}
