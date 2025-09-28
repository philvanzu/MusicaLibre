using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MusicaLibre.Services;
namespace MusicaLibre.Models;

public class Playlist
{
    public long DatabaseIndex { get; set; }
    public string FilePath { get; set; }=string.Empty;
    public string FileName { get; set; }=string.Empty;
    public string FolderPathstr { get; set; }=string.Empty;
    public Folder Folder { get; set; }
    public long FolderId { get; set; }
    public DateTime Modified { get; set; }
    public DateTime Created { get; set; }
    public DateTime Added { get; set; }
    public DateTime? Played { get; set; }
    public long? ArtworkId { get; set; }
    public Artwork? Artwork { get; set; }
    
    public List<(Track track, int position)> Tracks { get; set; } = new();
    
    const string selectSql = "SELECT * FROM Playlists;";
    const string deleteSql = "DELETE FROM Playlists WHERE Id = $id;";
    const string insertSql = @"
        INSERT INTO Playlists (FilePath, FileName, FolderId, Created, Modified)  
        VALUES ($filepath, $filename, $folderpath, $created, $modified);
        SELECT last_insert_rowid();";

    private Dictionary<string, object?> Parameters => new()
    {
        ["$id"] = DatabaseIndex,
        ["$filepath"] = FilePath,
        ["$filename"] = FileName,
        ["$folderpath"] = Folder?.DatabaseIndex,
        ["$created"] = TimeUtils.ToUnixTime(Created),
        ["$modified"] = TimeUtils.ToUnixTime(Modified),
        ["$artworkid"] = ArtworkId,
    };
    public void DatabaseInsert(Database db)
    {
        var playlistId = db.ExecuteScalar(insertSql, Parameters );
        DatabaseIndex = Convert.ToInt64(playlistId);
    }

    public async Task DatabaseInsertAsync(Database db)
    {
        var playlistId = await db.ExecuteScalarAsync(insertSql, Parameters );
        DatabaseIndex = Convert.ToInt64(playlistId);
    }
    
    public async Task DbDeleteAsync(Database db)
        => await db.ExecuteNonQueryAsync(deleteSql, Parameters );
    
    public static Dictionary<long, Playlist> FromDatabase(Database db)
        => ProcessReaderResult(db.ExecuteReader(selectSql));
    public static async Task<Dictionary<long, Playlist>> FromDatabaseAsync(Database db)
        => ProcessReaderResult(await db.ExecuteReaderAsync(selectSql));

    public static Dictionary<long, Playlist> ProcessReaderResult(List<Dictionary<string, object?>> result)
    {
        Dictionary<long, Playlist> playlists = new();
        foreach (var row in result)
        {
            var created = Convert.ToInt64(row["Created"]);
            var modified = Convert.ToInt64(row["Modified"]);
            var playlist = new Playlist()
            {
                DatabaseIndex = Convert.ToInt64(row["Id"]),
                FilePath = (string)row["FilePath"],
                FileName = (string)row["FileName"],
                FolderId = Convert.ToInt64(row["FolderId"]),
                Created = TimeUtils.FromUnixTime(created),
                Modified =TimeUtils.FromUnixTime(modified),
                ArtworkId = Database.GetValue<long>(row,  "ArtworkId"),
            };
            playlists.Add(playlist.DatabaseIndex, playlist);
        }


        return playlists;
    }

    public static List<string> Load(string filePath, Action<CueSheet> cueSheetHandler)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".m3u" or ".m3u8" => LoadM3U(filePath),
            ".pls"            => LoadPLS(filePath),
            ".wpl" or ".zpl"  => LoadWPL(filePath),
            ".cue"            => ParseCUE(filePath, cueSheetHandler),
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

    private static List<string> ParseCUE(string filePath, Action<CueSheet> cueSheetHandler)
    {
        var baseDir = Path.GetDirectoryName(filePath)!;
        var lines = File.ReadAllLines(filePath);

        var sheet = new CueSheet(){Path = filePath};
        string? currentFile = null;
        CueObject? current = sheet;
        CueIndex? previousIndex = null;
        uint trackId = 0;
        uint fileCount = 0;
        
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("FILE", StringComparison.OrdinalIgnoreCase))
            {
                // Match FILE "...." TYPE
                var match = Regex.Match(line, @"^FILE\s+""(.+?)""", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var path = NormalizePath(baseDir, match.Groups[1].Value);
                    currentFile = path;
                    trackId = 0;
                    previousIndex = null;
                }
                fileCount++;
            }
            else if (line.StartsWith("PERFORMER", StringComparison.OrdinalIgnoreCase))
            {
                current.Performer = line.Substring(9).Trim().Trim('"');
            }
            else if (line.StartsWith("TITLE", StringComparison.OrdinalIgnoreCase))
            {
                current.Title = line.Substring(5).Trim().Trim('"');
            }
            else if (line.StartsWith("TRACK", StringComparison.OrdinalIgnoreCase))
            {
                if (currentFile is null)
                {
                    Console.Error.WriteLine($"Inconsistent cue file {sheet.Path} // Couldnt determine Track File");
                    return new List<string>();
                }
                current = new CueTrack(currentFile) { Number = ++trackId, };
                sheet.Tracks.Add((CueTrack)current);
            }
            else if (line.StartsWith("INDEX", StringComparison.OrdinalIgnoreCase))
            {
                var timecode = line.Substring(8).Trim();
                var start = TimeUtils.ParseCueTime(timecode);
                if (current is CueTrack cueTrack)
                {
                    var index = new CueIndex(start);
                    cueTrack.Indexes.Add(index);
                    if(previousIndex is not null ) 
                        previousIndex.End = start;
                
                    previousIndex = index; 
                }
            }
        }

        if (sheet.Tracks.Count > fileCount)
        {
            cueSheetHandler.Invoke(sheet);
        }
        return sheet.Tracks.Select(t => t.File).Distinct().ToList();
    }
    private static string NormalizePath(string baseDir, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDir, path));
    }
}



public class CueSheet:CueObject
{
    public string Path { get; set; }
    public List<CueTrack> Tracks { get; set; } = new List<CueTrack>();

}

public abstract class CueObject
{
    public uint Number { get; set; }
    public string Title { get; set; } = "";
    public string Performer { get; set; } = "";
}

public class CueTrack: CueObject
{
    public string File { get; init; }
    public List<CueIndex> Indexes { get; set; } = new List<CueIndex>();

    public CueTrack(string file)
    {
        File = file;
    }
}

public class CueIndex
{
    public TimeSpan Start { get; init; }
    public TimeSpan? End { get; set; }

    public CueIndex(TimeSpan start)
    {
        Start = start;
    }
}