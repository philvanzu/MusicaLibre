using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using MusicaLibre.Services;

namespace MusicaLibre.Models;

public class Track
{
    // Identity
    public long? DatabaseIndex { get; set; } // Persistent internal ID
    public string? FilePath { get; set; } // Full path or UNC path
    public string? FileName { get; set; }
    
    public string FolderPathstr { get; set; }
    public Folder? Folder { get; set; }
    public long? FolderId { get; set; }
    public string? FileExtension { get; set; }

    // Core tags
    public string? Title { get; set; }
    public Album? Album { get; set; }
    public long? AlbumId { get; set; }
    public List<Artist> Artists { get; set; } = new ();
    public List<Artist> Composers { get; set; } = new();
    public Artist? Conductor { get; set; }
    public long? ConductorId { get; set; }
    public Artist? Remixer { get; set; }
    public long? RemixerId { get; set; }
    public Year? Year { get; set; }
    public long? YearId { get; set; }
    public uint? TrackNumber { get; set; }
    public uint? DiskNumber { get; set; }
    public List<Genre> Genres { get; set; } = new();
    public Publisher? Publisher { get; set; }
    public long? PublisherId { get; set; }
    public string? Comment { get; set; }
    public double? Rating { get; set; }

    // Technical
    public TimeSpan? Duration { get; set; }
    public int? BitrateKbps { get; set; }
    public string? Codec { get; set; } // e.g., FLAC, MP3, Opus
    public AudioFormat? AudioFormat { get; set; }
    public long? AudioFormatId { get; set; }
    public int? SampleRate { get; set; }
    public int? Channels { get; set; }

    // Artwork (optional path or cached blob)
    public List<Artwork> Artworks { get; set; } = new();
    

    // State info
    public DateTime? DateAdded { get; set; }
    public DateTime? LastPlayed { get; set; }
    public DateTime? Modified { get; set; }
    public DateTime? Created { get; set; }
    public int? PlayCount { get; set; }
    

    // Flags
    public bool IsMissing { get; set; } // File not found
    
    public void DatabaseInsert(Database db)
    {

        const string sql = @"
        INSERT INTO Tracks (FilePath, FileName, FolderId, FileExtension, Title, YearId, TrackNumber, DiscNumber, Duration, 
                            Codec, Bitrate, AudioFormatId, SampleRate, Added, Modified, Created, LastPlayed, AlbumId, 
                            PublisherId, ConductorId, RemixerId, Comments, Rating)
        VALUES ($filepath, $filename, $folderpath, $extension, $title, $year, $tracknumber, $discnumber, $duration, 
                $codec, $bitrate, $format, $samplerate, $added, $modified, $created, $lastplayed, $albumid, 
                $publisherid, $conductorid, $remixerid, $comments, $rating);
        SELECT last_insert_rowid();";

        var id = db.ExecuteScalar(sql, new()
        {
            ["$filepath"] = FilePath,
            ["$filename"]=FileName,
            ["$folderpath"]=Folder?.DatabaseIndex,
            ["$extension"]=FileExtension,
            ["$title"] = Title,
            ["$year"] = Year?.DatabaseIndex,
            ["$tracknumber"]=TrackNumber,
            ["$discnumber"] = DiskNumber,
            ["$duration"] = Duration.HasValue? TimeUtils.ToMilliseconds(Duration.Value):null,
            ["$codec"] = Codec,
            ["$bitrate"] = BitrateKbps,
            ["$format"] = AudioFormat?.DatabaseIndex,
            ["$samplerate"] = SampleRate,
            ["$added"] =  DateAdded.HasValue ? TimeUtils.ToUnixTime(DateAdded.Value):null,
            ["$modified"] = Modified.HasValue ? TimeUtils.ToUnixTime(Modified.Value):null,
            ["$created"] = Created.HasValue ? TimeUtils.ToUnixTime(Created.Value):null,
            ["$lastplayed"] = LastPlayed.HasValue ? TimeUtils.ToUnixTime(LastPlayed.Value):null, 
            ["$albumid"] = Album?.DatabaseIndex, 
            ["$publisherid"] = Publisher?.DatabaseIndex, 
            ["$conductorid"] = Conductor?.DatabaseIndex,
            ["$remixerid"] = Remixer?.DatabaseIndex,
            ["$comments"] = Comment,
            ["$rating"] = Rating,
            
        });

        DatabaseIndex =  Convert.ToInt64(id);
    }
    
    public static Dictionary<long, Track> FromDatabase(Database db, int[]? indexes = null)
    {
        string filter = String.Empty;
        if (indexes != null && indexes.Length == 0)
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";

        string sql = $@"SELECT * FROM Tracks {filter};";

        Dictionary<long, Track> tracks = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            
            var  modified = Database.GetValue<long>(row, "Modified");
            var lastPlayed = Database.GetValue<long>(row, "LastPlayed");
            var created = Database.GetValue<long>(row, "Created");
            var added = Database.GetValue<long>(row, "Added");
            var duration = Database.GetValue<double>(row, "Duration");
            Track track = new Track()
            {
                DatabaseIndex = Database.GetValue<long>(row, "Id"),
                AlbumId = Database.GetValue<long>(row, "AlbumId"),
                PublisherId = Database.GetValue<long>(row, "PublisherId"),
                RemixerId = Database.GetValue<long>(row, "RemixerId"),
                ConductorId = Database.GetValue<long>(row, "ConductorId"),
                FilePath = Database.GetString(row, "FilePath"),
                FileName = Database.GetString(row, "FileName"),
                FolderId = Database.GetValue<long>(row, "FolderId"),
                Title = Database.GetString(row, "Title"),
                YearId = Database.GetValue<long>(row, "YearId"),
                TrackNumber = Database.GetValue<uint>(row, "TrackNumber"),
                DiskNumber = Database.GetValue<uint>(row, "DiscNumber"),
                Duration = duration !=null ? TimeUtils.FromMilliseconds(duration.Value):null,
                BitrateKbps = Database.GetValue<int>(row, "Bitrate"),
                AudioFormatId =  Database.GetValue<long>(row, "AudioFormatId"), 
                SampleRate = Database.GetValue<int>(row, "SampleRate"),
                DateAdded = added!=null ? TimeUtils.FromUnixTime(added.Value) : null,
                Modified = modified!=null ? TimeUtils.FromUnixTime(modified.Value) : null,
                Created = created!=null ? TimeUtils.FromUnixTime(created.Value) : null,
                LastPlayed = lastPlayed!=null ? TimeUtils.FromUnixTime(lastPlayed.Value) : null,
                Comment = Database.GetString(row, "Comments"),
                Rating = Database.GetValue<double>(row, "Rating"),
                PlayCount = Database.GetValue<int>(row, "PlayCount"),
            };
            tracks.Add(track.DatabaseIndex!.Value, track);
        }

        return tracks;
    }
}