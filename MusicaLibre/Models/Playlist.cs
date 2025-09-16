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
    
    public List<(Track track, int position)> Tracks { get; set; } = new();
    
    const string selectSql = "SELECT * FROM Playlists;";
    const string insertSql = @"INSERT INTO Playlists (FilePath, FileName, FolderId, Created, Modified)  
                VALUES ($filepath, $filename, $folderpath, $created, $modified);
                SELECT last_insert_rowid();";

    private Dictionary<string, object?> Parameters => new()
    {
        ["$filepath"] = FilePath,
        ["$filename"] = FileName,
        ["$folderpath"] = Folder?.DatabaseIndex,
        ["$created"] = TimeUtils.ToUnixTime(Created),
        ["$modified"] = TimeUtils.ToUnixTime(Modified),
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

    private static List<string> ParseCUE(string filePath, Action<CueSheet> cueSheetHandler)
    {
        var baseDir = Path.GetDirectoryName(filePath)!;
        var lines = File.ReadAllLines(filePath);

        string currentFile = "";
        string currentPerformer = "";
        string currentTitle = "";
        var sheet = new CueSheet(){Path = filePath};
        CueSheet.CueTrack? previousTrack = null;
        bool trackLevel = false;
        uint trackNumber = 0;
        List<string> files = new();
        
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("FILE", StringComparison.OrdinalIgnoreCase))
            {
                trackLevel = false;

                // Match FILE "...." TYPE
                var match = Regex.Match(line, @"^FILE\s+""(.+?)""", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    currentFile = NormalizePath(baseDir, match.Groups[1].Value);
                    files.Add(currentFile);
                }
            }
            else if (line.StartsWith("PERFORMER", StringComparison.OrdinalIgnoreCase))
            {
                currentPerformer = line.Substring(9).Trim().Trim('"');
                if(!trackLevel) sheet.Performer = currentPerformer;
            }
            else if (line.StartsWith("TITLE", StringComparison.OrdinalIgnoreCase))
            {
                currentTitle = line.Substring(5).Trim().Trim('"');
                if(!trackLevel) sheet.Title = currentTitle;
            }
            else if (line.StartsWith("TRACK", StringComparison.OrdinalIgnoreCase))
            {
                // reset track-level metadata (will be overridden by later lines)
                currentTitle = "";
                currentPerformer = "";
                trackLevel = true;
                trackNumber++;
            }
            else if (line.StartsWith("INDEX 01", StringComparison.OrdinalIgnoreCase))
            {
                var timecode = line.Substring(8).Trim();
                var start = TimeUtils.ParseCueTime(timecode);

                var ctrack = new CueSheet.CueTrack
                {
                    File = currentFile,
                    Performer = currentPerformer,
                    Title = currentTitle,
                    Start = start,
                    Number = trackNumber,
                };
                sheet.Tracks.Add(ctrack);
                
                if(previousTrack is not null ) 
                    previousTrack.End = ctrack.Start;
                
                previousTrack = ctrack; 
            }
        }

        if (sheet.Tracks.Count > files.Count)
        {
            cueSheetHandler.Invoke(sheet);
        }
        return files;
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

public class CueSheet
{
    public string Path { get; set; }
    public string Title { get; set; }
    public string Performer { get; set; }
    
    public List<CueTrack> Tracks { get; set; } = new List<CueTrack>();
    public class CueTrack
    {
        public string File { get; set; } = "";
        public uint Number { get; set; }
        public string Title { get; set; } = "";
        public string Performer { get; set; } = "";
        public TimeSpan Start { get; set; }
        public TimeSpan? End { get; set; }
    }
}