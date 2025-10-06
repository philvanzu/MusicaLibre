using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MusicaLibre.Services;

public static class PathUtils
{
    public static string NormalizePath(string baseDir, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDir, path));
    }
    public static string GetRelativePath(string rootPath, string path)
    {
        return Path.GetRelativePath(rootPath, path);
    }
    
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
