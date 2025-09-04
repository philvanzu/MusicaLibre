using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using MusicaLibre.Services;
namespace MusicaLibre.Models;

public class Playlist
{
    public long? DatabaseIndex { get; set; }
    public string? FilePath { get; set; }
    public string? FileName { get; set; }
    public string? FolderPathstr { get; set; }
    public Folder? Folder { get; set; }
    public long? FolderId { get; set; }
    public DateTime? Modified { get; set; }
    public DateTime? Created { get; set; }
    public DateTime? Added { get; set; }
    public DateTime? Played { get; set; }

    public List<(Track track, int position)> Tracks { get; set; } = new();


    public void DatabaseInsert(Database db)
    {
        var sql = @"INSERT INTO Playlists (FilePath, FileName, FolderId, Created, Modified)  
                VALUES ($filepath, $filename, $folderpath, $created, $modified);
                SELECT last_insert_rowid();";
        var playlistId = db.ExecuteScalar(sql, new ()
        {
            ["$filepath"]  = FilePath,
            ["$filename"]  = FileName,
            ["$folderpath"] = Folder?.DatabaseIndex,
            ["$created"]   = Created.HasValue?TimeUtils.ToUnixTime(Created.Value):null,
            ["$modified"]  = Modified.HasValue?TimeUtils.ToUnixTime(Modified.Value):null,
        });
        DatabaseIndex = Convert.ToInt64(playlistId);
    }

    public static Dictionary<long, Playlist> FromDatabase(Database db, long[]? indexes = null)
    {
        string filter = String.Empty;
        if (indexes != null && indexes.Length == 0)
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";

        string sql = $@"SELECT * FROM Playlists {filter};";

        Dictionary<long, Playlist> playlists = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            var created = Database.GetValue<long>(row, "Created");
            var modified = Database.GetValue<long>(row, "Modified");
            var playlist = new Playlist()
            {
                DatabaseIndex = Convert.ToInt64(row["Id"]),
                FilePath = Database.GetString(row, "FilePath"),
                FileName = Database.GetString(row, "FileName"),
                FolderId = Database.GetValue<long>(row, "FolderId"),
                Created = created.HasValue ? TimeUtils.FromUnixTime(created.Value) : null,
                Modified = modified.HasValue ? TimeUtils.FromUnixTime(modified.Value) : null,
            };
            playlists.Add(playlist.DatabaseIndex.Value, playlist);
        }


        return playlists;
    }

    public static List<string> Load(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".m3u" or ".m3u8" => LoadM3U(filePath),
            ".pls"            => LoadPLS(filePath),
            ".wpl" or ".zpl"  => LoadWPL(filePath),
            ".cue"            => LoadCUE(filePath),
            ".xspf"           => LoadXSPF(filePath),
            _                 => throw new NotSupportedException($"Unsupported playlist format: {ext}")
        };
    }

    private static List<string> LoadM3U(string filePath)
    {
        var baseDir = Path.GetDirectoryName(filePath)!;
        return File.ReadAllLines(filePath)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
            .Select(l => NormalizePath(baseDir, l))
            .ToList();
    }

    private static List<string> LoadPLS(string filePath)
    {
        var baseDir = Path.GetDirectoryName(filePath)!;
        return File.ReadAllLines(filePath)
            .Where(l => l.StartsWith("File", StringComparison.OrdinalIgnoreCase))
            .Select(l => l.Split('=', 2)[1].Trim())
            .Select(l => NormalizePath(baseDir, l))
            .ToList();
    }

    private static List<string> LoadWPL(string filePath)
    {
        var baseDir = Path.GetDirectoryName(filePath)!;
        var doc = XDocument.Load(filePath);

        return doc.Descendants()
            .Where(e => e.Name.LocalName == "media")
            .Select(e => e.Attribute("src")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => NormalizePath(baseDir, v!))
            .ToList();
    }
    private static List<string> LoadCUE(string filePath)
    {
        var baseDir = Path.GetDirectoryName(filePath)!;
        var lines = File.ReadAllLines(filePath);

        return lines
            .Where(l => l.TrimStart().StartsWith("FILE", StringComparison.OrdinalIgnoreCase))
            .Select(l =>
            {
                // CUE "FILE filename.ext WAVE"
                var parts = l.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var candidate = parts[1].Trim('"');
                    return NormalizePath(baseDir, candidate);
                }
                return null;
            })
            .Where(p => p != null)
            .Cast<string>()
            .Distinct()
            .ToList();
    }

    private static List<string> LoadXSPF(string filePath)
    {
        var baseDir = Path.GetDirectoryName(filePath)!;
        var doc = XDocument.Load(filePath);

        return doc.Descendants()
            .Where(e => e.Name.LocalName == "location")
            .Select(e => e.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v =>
            {
                if (Uri.TryCreate(v, UriKind.Absolute, out var uri) && uri.IsFile)
                    return Path.GetFullPath(Uri.UnescapeDataString(uri.LocalPath));
                return NormalizePath(baseDir, v);
            })
            .ToList();
    }

    private static string NormalizePath(string baseDir, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDir, path));
    }
}