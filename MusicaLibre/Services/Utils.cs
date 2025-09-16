using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MusicaLibre.Models;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Services;

public static class PathUtils
{
    static readonly HashSet<string> AudioFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core formats
        ".mp3", ".wav", ".aif", ".aiff", ".ogg",

        // Lossless
        ".flac", ".alac", // ALAC inside .m4a
        ".ape",           // Monkey's Audio
        ".wv",            // WavPack

        // AAC & variants
        ".aac", ".m4a", ".mp4", ".m4b", // AAC/ALAC container formats

        // Microsoft
        ".wma",

        // Modern streaming codecs
        ".opus",

        // Older scene/high quality
        ".mpc", ".mp+",   // Musepack extensions

        // Instrumental/sequence formats
        ".mid", ".midi",

        // Audiophile niche
        ".dsd", ".dsf", ".dff"
    };
    public static bool IsAudioFile(string path)
    {
        return AudioFileExtensions.Contains(Path.GetExtension(path));
    }

    static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp"
    };
    public static bool IsImage(string path)
    {
        return ImageExtensions.Contains(Path.GetExtension(path));
    }
    
    public static string? GetMimeType(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext))
            return null;

        ext = ext.Trim().ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"            => "image/png",
            ".gif"            => "image/gif",
            ".bmp"            => "image/bmp",
            ".webp"           => "image/webp",
            ".tif" or ".tiff" => "image/tiff",
            _                 => null
        };    
    }

    private static readonly HashSet<string> PlaylistExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".m3u", ".m3u8", ".pls", ".cue", ".wpl", ".zpl", ".xspf"
    };
    public static bool IsPlaylist(string path)
    {
        return PlaylistExtensions.Contains(Path.GetExtension(path));
    }
    

    

    public static bool IsDescendantPath(string parentPath, string childPath)
    {
        var parentUri = new Uri(AppendDirectorySeparator(parentPath), UriKind.Absolute);
        var childUri = new Uri(AppendDirectorySeparator(childPath), UriKind.Absolute);
        return parentUri.IsBaseOf(childUri);
    }
    private static string AppendDirectorySeparator(string path)
    {
        return path.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString())
            ? path
            : path + System.IO.Path.DirectorySeparatorChar;
    }
    
    public static bool CouldBeDirectory(string path)
    {
        return string.IsNullOrEmpty(Path.GetExtension(path));
    }

    public static bool IsWatchable(string path) => IsAudioFile(path);

    public static bool IsFileReady(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string? GetCommonRoot(IEnumerable<string> paths)
    {
        if (paths == null) throw new ArgumentNullException(nameof(paths));
        var pathList = paths.Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar))
            .ToList();

        if (pathList.Count == 0)
            return null;

        // Split each path into parts
        var splitPaths = pathList
            .Select(p => p.Split(Path.DirectorySeparatorChar))
            .ToList();

        // Find the minimum length among them
        int minLen = splitPaths.Min(p => p.Length);
        var commonParts = new List<string>();

        for (int i = 0; i < minLen; i++)
        {
            string candidate = splitPaths[0][i];
            if (splitPaths.All(p => string.Equals(p[i], candidate, StringComparison.OrdinalIgnoreCase)))
            {
                commonParts.Add(candidate);
            }
            else
            {
                break;
            }
        }

        if (commonParts.Count == 0)
            return null;

        return string.Join(Path.DirectorySeparatorChar, commonParts);
    }

    public static string GetRelativePath(string rootPath, string path)
    {
        throw new NotImplementedException();
    }

    public static void OpenInExplorer(string filePath)
    {
        bool fileExists = File.Exists(filePath);
        bool directoryExists = Directory.Exists(filePath);
        var directoryPath = directoryExists? filePath : Path.GetDirectoryName(filePath);

        if (fileExists)
        {
            string? desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")
                              ?? Environment.GetEnvironmentVariable("DESKTOP_SESSION")
                              ?? string.Empty;
            desktop = desktop.ToLowerInvariant();
                
            //GNOME (Nautilus)
            if (desktop.Contains("gnome") || desktop.Contains("unity") || desktop.Contains("cinnamon"))
                Process.Start("nautilus", $"--select \"{filePath}\"");
            //Dolphin (KDE)
            else if (desktop.Contains("kde"))
                Process.Start("dolphin", $"--select \"{filePath}\"");
            // Try with xdg-open (common across most Linux desktop environments)
            else
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{directoryPath}\"") { UseShellExecute = true });    
        }
        else
        {
            Process.Start(new ProcessStartInfo("xdg-open", $"\"{directoryPath}\"") { UseShellExecute = true });
        }
                
    }

    
}

public static class EnumUtils
{
    public static string[] GetDisplayNames<TEnum>() where TEnum : Enum
    {
        return typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f =>
            {
                var attr = f.GetCustomAttribute<DisplayAttribute>();
                return attr?.Name ?? f.Name;
            })
            .ToArray();
    }
    public static string GetDisplayName(Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        if (field is null)
            return value.ToString();

        var attr = field.GetCustomAttribute<DisplayAttribute>();
        return attr?.Name ?? value.ToString();
    }
}

public static class TimeUtils
{
    public static long ToUnixTime(DateTime dt) =>
        ((DateTimeOffset)dt.ToUniversalTime()).ToUnixTimeSeconds();

    public static DateTime FromUnixTime(long ts) =>
        DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;
    
    public static double ToMilliseconds(TimeSpan ts)=>ts.TotalMilliseconds;
    public static TimeSpan FromMilliseconds(double ts) => TimeSpan.FromMilliseconds(ts);
    
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return duration.ToString(@"hh\:mm\:ss");   // 1:23:45
        else
            return duration.ToString(@"m\:ss");       // 4:05
    }

    public static string FormatDate(DateTime? dt)
    {
        return dt?.ToString("yy-MM-dd") ?? string.Empty;
    }
    public static DateTime? FromDateString(string? s)
    {
        if (DateTime.TryParseExact(
                s,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
            return parsed;
        
        return null;
    }
    public static string FormatDateTime(DateTime? dt)
    {
        return dt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
    }

    public static DateTime? FromDateTimeString(string? s)
    {
        if (DateTime.TryParseExact(
                s,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
            return parsed;
        
        return null;
    }

    public static DateTime Earliest(IEnumerable<DateTime> array)
    {
        var ret = DateTime.MaxValue;
        foreach (var date in array)
            if (date !=null && date < ret)
                ret = date;
        
        return ret;
    }

    public static DateTime Latest(IEnumerable<DateTime> array)
    {
        var ret = DateTime.MinValue;
        foreach (var date in array)
            if (date > ret)
                ret = date;
        return ret;
    }
    
    public static TimeSpan ParseCueTime(string timecode)
    {
        // mm:ss:ff (75 frames per second)
        var parts = timecode.Split(':');
        if (parts.Length != 3) return TimeSpan.Zero;

        int mm = int.Parse(parts[0]);
        int ss = int.Parse(parts[1]);
        int ff = int.Parse(parts[2]);

        return TimeSpan.FromSeconds(mm * 60 + ss + ff / 75.0);
    }

    public static (double start, double end) GetCueTrackTimes(TimeSpan tStart, TimeSpan? tEnd, TimeSpan tDuration)
    {
        if (tDuration.Ticks <= 0)
            return (0, 1);
        
        var start= (double) tStart.Ticks / tDuration.Ticks;
        var end = tEnd.HasValue ? (double)tEnd.Value.Ticks / tDuration.Ticks : 1;
        start = Math.Clamp(start, 0, 1);
        end   = Math.Clamp(end, start, 1);
        return (start, end);
    }
}

public static class UnicodeUtils
{
    
    public static string asc = "⌃"; //⮝ ⮟ ‹›⥊ ⥋ ⥌⥎⥍ , ⥎ ↑↓  
    public static string desc = "⌄";
    public static string hamburger = "☰";
    public static string play = "▶";
}

public static class SearchUtils
{
    public static double GetScore(string[] searchTokens, string[] fieldTokens, double weight)
    {
        double score = 0.0;
        var tokens = fieldTokens.ToList(); // mutable copy
        double penalty = weight * 0.05;   // 5% of the field weight

        foreach (var search in searchTokens)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                if (string.Equals(tokens[i], search, StringComparison.OrdinalIgnoreCase))
                {
                    score += weight * 2;   // exact match bonus
                    tokens.RemoveAt(i);
                    break;
                }
                else if (tokens[i].Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    score += weight;       // partial match bonus
                    tokens.RemoveAt(i);
                    break;
                }
            }
        }

        // Penalize remaining unmatched tokens
        score -= tokens.Count * penalty;

        return score;
    }

    public static Dictionary<Track, double> FilterTracks(string searchString, List<Track> tracks, LibraryViewModel library)
    {
        var splits = searchString.Split(' ').Select(x => x.Trim()).ToArray();
        
        var weights =  new Dictionary<Track, double>();
        var artistWeights = new Dictionary<Artist, double>();

        foreach (var artist in library.Data.Artists.Values)
        {
            string[] arr = { artist.Name };
            var aw = GetScore(splits, arr, 1.0);
            if (aw > 0)
                artistWeights[artist] = artistWeights.GetValueOrDefault(artist) + aw;
        }
        // Title matches – strongest
        foreach (var track in tracks)
        {
            
            var titleweight = string.IsNullOrWhiteSpace(track.Title)? 0 :
                GetScore(splits, track.Title.Split(' ').Select(x => x.Trim()).ToArray(), 1.0);

            var albumweight = track.Album is null ? 0 : 
                GetScore(splits, track.Album.Title.Split(' ').Select(x => x.Trim()).ToArray(), 1.0);

            double artistweight = 0;
            foreach(var kv in artistWeights)
                if(track.Artists.Contains(kv.Key))
                    artistweight += kv.Value;
                
            var weight = Math.Max(artistweight, Math.Max(titleweight, albumweight));
            if (weight > 0)
                weights[track] = weights.GetValueOrDefault(track) + weight;    
        }
        return weights;
    }

}


