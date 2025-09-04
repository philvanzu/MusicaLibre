using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;

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
        new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();

    public static DateTime FromUnixTime(long ts) =>
        DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;
    
    public static double ToMilliseconds(TimeSpan ts)=>ts.TotalMilliseconds;
    public static TimeSpan FromMilliseconds(double ts) => TimeSpan.FromMilliseconds(ts);
    
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return duration.ToString(@"h\:mm\:ss");   // 1:23:45
        else
            return duration.ToString(@"m\:ss");       // 4:05
    }

    public static string FormatDate(DateTime? dt)
    {
        return dt?.ToString("yy-MM-dd") ?? string.Empty;
    }
}

public static class UnicodeUtils
{
    
    public static string asc = "⌃"; //⮝ ⮟ ‹›⥊ ⥋ ⥌ ⥍ , ⥎ ↑↓  
    public static string desc = "⌄";
    public static string hamburger = "☰";
    public static string play = "▶";
}
