using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using DynamicData.Binding;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Models;

public class Track
{
    // Identity
    public long DatabaseIndex { get; set; } // Persistent internal ID
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
    public uint TrackNumber { get; set; }
    public uint DiscNumber { get; set; }
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
    
    public void DbInsert(Database db)
    {
        var id = db.ExecuteScalar(insertSql, Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
    }

    public async Task DbInsertAsync(Database db, Action<long>? callback = null)
    {
        try
        {
            var id = await db.ExecuteScalarAsync(insertSql, Parameters);
            DatabaseIndex =  Convert.ToInt64(id);
            callback?.Invoke(DatabaseIndex);    
        }
        catch(Exception e){Console.WriteLine(e);}
    }
    
    public void DbUpdate(Database db)=>db.ExecuteNonQuery(updateSql, Parameters);
    public async Task DbUpdateAsync(Database db)
    {
        try
        {
            await db.ExecuteNonQueryAsync(updateSql, Parameters);    
        }
        catch(Exception e){Console.WriteLine(e);}
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
                DatabaseIndex = Convert.ToInt64(row["Id"]),
                AlbumId = Database.GetValue<long>(row, "AlbumId"),
                PublisherId = Database.GetValue<long>(row, "PublisherId"),
                RemixerId = Database.GetValue<long>(row, "RemixerId"),
                ConductorId = Database.GetValue<long>(row, "ConductorId"),
                FilePath = Database.GetString(row, "FilePath"),
                FileName = Database.GetString(row, "FileName"),
                FolderId = Database.GetValue<long>(row, "FolderId"),
                FileExtension = Database.GetString(row, "FileExtension"),
                Title = Database.GetString(row, "Title"),
                YearId = Database.GetValue<long>(row, "YearId"),
                TrackNumber = Convert.ToUInt32(row["TrackNumber"]),
                DiscNumber = Convert.ToUInt32(row["DiscNumber"]),
                Duration = duration !=null ? TimeUtils.FromMilliseconds(duration.Value):null,
                Codec = Database.GetString(row, "Codec"),
                BitrateKbps = Database.GetValue<int>(row, "Bitrate"),
                SampleRate = Database.GetValue<int>(row, "SampleRate"),
                Channels = Database.GetValue<int>(row, "Channels"),
                AudioFormatId =  Database.GetValue<long>(row, "AudioFormatId"), 
                DateAdded = added!=null ? TimeUtils.FromUnixTime(added.Value) : null,
                Modified = modified!=null ? TimeUtils.FromUnixTime(modified.Value) : null,
                Created = created!=null ? TimeUtils.FromUnixTime(created.Value) : null,
                LastPlayed = lastPlayed!=null ? TimeUtils.FromUnixTime(lastPlayed.Value) : null,
                Comment = Database.GetString(row, "Comments"),
                Rating = Database.GetValue<double>(row, "Rating"),
                PlayCount = Database.GetValue<int>(row, "PlayCount"),
            };
            tracks.Add(track.DatabaseIndex, track);
        }

        return tracks;
    }
    
    private const string insertSql= @"
        INSERT INTO Tracks (FilePath, FileName, FolderId, FileExtension, Title, YearId, TrackNumber, DiscNumber, Duration, 
                            Codec, Bitrate, AudioFormatId, SampleRate, Channels, Added, Modified, Created, LastPlayed, AlbumId, 
                            PublisherId, ConductorId, RemixerId, Comments, Rating)
        VALUES ($filepath, $filename, $folderId, $ext, $title, $yearId, $tracknumber, $disc, $duration, 
                $codec, $bitrate, $format, $samplerate, $channels, $added, $modified, $created, $played, $albumId, 
                $publisherId, $conductorId, $remixerId, $comments, $rating);
        SELECT last_insert_rowid();";

    private const string updateSql = @"
        UPDATE Tracks SET
            FilePath = $filepath, 
            FileName = $filename, 
            FolderId = $folderId,
            FileExtension=$ext, 
            Title = $title, 
            YearId = $yearId, 
            TrackNumber = $tracknumber, 
            DiscNumber = $disc, 
            Duration = $duration, 
            Codec = $codec, 
            Bitrate = $bitrate, 
            AudioFormatId = $format, 
            SampleRate = $samplerate, 
            Channels= $channels, 
            Added = $added, 
            Modified = $modified, 
            Created = $created, 
            LastPlayed=$played, 
            AlbumId= $albumId, 
            PublisherId = $publisherId, 
            ConductorId = $conductorId, 
            RemixerId = $remixerId, 
            Comments = $comments, 
            Rating = $rating
        WHERE Id = $id;";

    public Dictionary<string, object?> Parameters => new()
    {
        ["$filepath"] = FilePath,
        ["$filename"]=FileName,
        ["$folderId"]=Folder?.DatabaseIndex,
        ["$ext"]=FileExtension,
        ["$title"] = Title,
        ["$yearId"] = Year?.DatabaseIndex,
        ["$tracknumber"]=TrackNumber,
        ["$disc"] = DiscNumber,
        ["$duration"] = Duration.HasValue? TimeUtils.ToMilliseconds(Duration.Value):null,
        ["$codec"] = Codec,
        ["$bitrate"] = BitrateKbps,
        ["$format"] = AudioFormat?.DatabaseIndex,
        ["$samplerate"] = SampleRate,
        ["$channels"] = Channels,
        ["$added"] =  DateAdded.HasValue ? TimeUtils.ToUnixTime(DateAdded.Value):null,
        ["$modified"] = Modified.HasValue ? TimeUtils.ToUnixTime(Modified.Value):null,
        ["$created"] = Created.HasValue ? TimeUtils.ToUnixTime(Created.Value):null,
        ["$played"] = LastPlayed.HasValue ? TimeUtils.ToUnixTime(LastPlayed.Value):null, 
        ["$albumId"] = Album?.DatabaseIndex, 
        ["$publisherId"] = Publisher?.DatabaseIndex, 
        ["$conductorId"] = Conductor?.DatabaseIndex,
        ["$remixerId"] = Remixer?.DatabaseIndex,
        ["$comments"] = Comment,
        ["$rating"] = Rating,
        ["$id"] = DatabaseIndex
    };
    
    
    public async Task UpdateGenresAsync(LibraryViewModel library)
    {
        try
        {
            string delete = $"DELETE FROM TrackGenres WHERE TrackId = {DatabaseIndex};";
            await library.Database.ExecuteNonQueryAsync(delete);
        
            string insert = "INSERT INTO TrackGenres (TrackId, GenreId) VALUES ($id, $genreId);";
            foreach( var genre in Genres .Select(x=>x.DatabaseIndex))
                if (genre.HasValue)
                {
                    _ = library.Database.ExecuteNonQueryAsync(insert, new()
                    {
                        ["$id"] = DatabaseIndex,
                        ["$genreId"] = genre.Value,
                    });
                }    
        }
        catch(Exception e){Console.WriteLine(e);}
    } 

    /*
    public static Track Null = new Track()
    {
        Title = "Null",
        FilePath = "Null",
        FileName = "Null",
        Folder = Folder.Null,
        Year = Year.Null,
        Album = Album.Null,
    };
    */
}