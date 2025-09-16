using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Models;

public class Track
{
    // Identity
    public long DatabaseIndex { get; private set; } // Persistent internal ID
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    
    public string FolderPathstr { get; set; } = string.Empty;
    public Folder Folder { get; set; }
    public long FolderId { get; set; }
    public string FileExtension { get; set; } = string.Empty;

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
    public Year Year { get; set; }
    public long YearId { get; set; }
    public uint TrackNumber { get; set; }
    public uint DiscNumber { get; set; }
    public List<Genre> Genres { get; set; } = new();
    public Publisher? Publisher { get; set; }
    public long? PublisherId { get; set; }
    public string Comment { get; set; }=string.Empty;
    public double? Rating { get; set; }

    // Technical
    public TimeSpan Duration { get; set; }
    public double Start { get; set; } = 0;
    public double End { get; set; } = 1;
    public int BitrateKbps { get; set; }
    public string Codec { get; set; } = string.Empty;  // e.g., FLAC, MP3, Opus
    public AudioFormat AudioFormat { get; set; }
    public long AudioFormatId { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    
    

    // Artwork (optional path or cached blob)
    public List<Artwork> Artworks { get; set; } = new();
    

    // State info
    public DateTime DateAdded { get; set; }
    public DateTime? LastPlayed { get; set; }
    public DateTime Modified { get; set; }
    public DateTime Created { get; set; }
    public int PlayCount { get; set; }

    public static Track Copy (Track other) => (Track)other.MemberwiseClone();
    // Flags
    public bool IsMissing { get; set; } // File not found
    
    public void DbInsert(Database db)
    {
        var id = db.ExecuteScalar(insertSql, Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
    }
    private const string insertSql= @"
        INSERT INTO Tracks (FilePath, FileName, FolderId, FileExtension, Title, YearId, TrackNumber, DiscNumber, Duration, Start, End, 
                            Codec, Bitrate, AudioFormatId, SampleRate, Channels, Added, Modified, Created, LastPlayed, AlbumId, 
                            PublisherId, ConductorId, RemixerId, Comments, Rating)
        VALUES ($filepath, $filename, $folderId, $ext, $title, $yearId, $tracknumber, $disc, $duration, $start, $end,
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
            Start = $start,
            End = $end,
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

    private Dictionary<string, object?> Parameters => new()
    {
        ["$filepath"] = FilePath,
        ["$filename"]=FileName,
        ["$folderId"]=Folder.DatabaseIndex,
        ["$ext"]=FileExtension,
        ["$title"] = Title,
        ["$yearId"] = Year.DatabaseIndex,
        ["$tracknumber"]=TrackNumber,
        ["$disc"] = DiscNumber,
        ["$duration"] = TimeUtils.ToMilliseconds(Duration),
        ["$start"] = Start,
        ["$end"] = End,
        ["$codec"] = Codec,
        ["$bitrate"] = BitrateKbps,
        ["$format"] = AudioFormat.DatabaseIndex,
        ["$samplerate"] = SampleRate,
        ["$channels"] = Channels,
        ["$added"] =  TimeUtils.ToUnixTime(DateAdded),
        ["$modified"] = TimeUtils.ToUnixTime(Modified),
        ["$created"] = TimeUtils.ToUnixTime(Created),
        ["$played"] = LastPlayed.HasValue ? TimeUtils.ToUnixTime(LastPlayed.Value):null, 
        ["$albumId"] = Album?.DatabaseIndex, 
        ["$publisherId"] = Publisher?.DatabaseIndex, 
        ["$conductorId"] = Conductor?.DatabaseIndex,
        ["$remixerId"] = Remixer?.DatabaseIndex,
        ["$comments"] = Comment,
        ["$rating"] = Rating,
        ["$id"] = DatabaseIndex
    };
    
    const string deleteSql="DELETE FROM Tracks WHERE Id = $id;";
    const string selectSql = "SELECT * FROM Tracks;";
    
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
    
    public void DbDelete(Database db)=>db.ExecuteNonQuery(deleteSql, Parameters);
    public async Task DbDeleteAsync(Database db)=>await db.ExecuteNonQueryAsync(deleteSql, Parameters);

    public static Dictionary<long, Track> FromDatabase(Database db)
        => ProcessReaderResult( db.ExecuteReader(selectSql));
    
    public static async Task<Dictionary<long, Track>> FromDatabaseAsync(Database db)
        => ProcessReaderResult(await db.ExecuteReaderAsync(selectSql));

    public static Dictionary<long, Track> ProcessReaderResult(List<Dictionary<string, object?>> result)
    {
        Dictionary<long, Track> tracks = new();
        foreach (var row in result)
        {
            
            var  modified = Convert.ToInt64(row["Modified"]);
            var lastPlayed = Database.GetValue<long>(row, "LastPlayed");
            var created = Convert.ToInt64(row["Created"]);
            var added = Convert.ToInt64(row["Added"]);
            var duration = Convert.ToInt64(row["Duration"]);
            Track track = new Track()
            {
                DatabaseIndex = Convert.ToInt64(row["Id"]),
                AlbumId = Database.GetValue<long>(row, "AlbumId"),
                PublisherId = Database.GetValue<long>(row, "PublisherId"),
                RemixerId = Database.GetValue<long>(row, "RemixerId"),
                ConductorId = Database.GetValue<long>(row, "ConductorId"),
                FilePath = (string?) row["FilePath"]??String.Empty,
                FileName = (string?) row["FileName"]??String.Empty,
                FolderId = Convert.ToInt64(row["FolderId"]),
                FileExtension = (string?) row["FileExtension"]??String.Empty,
                Title = Database.GetString(row, "Title"),
                YearId = Convert.ToInt64(row["YearId"]),
                TrackNumber = Convert.ToUInt32(row["TrackNumber"]),
                DiscNumber = Convert.ToUInt32(row["DiscNumber"]),
                Duration = TimeUtils.FromMilliseconds(duration),
                Start = Convert.ToDouble(row["Start"]),
                End = Convert.ToDouble(row["End"]),
                Codec = (string?)row["Codec"]??string.Empty,
                BitrateKbps = Convert.ToInt32(row["Bitrate"]),
                SampleRate = Convert.ToInt32(row["SampleRate"]),
                Channels = Convert.ToInt32(row["Channels"]),
                AudioFormatId =  Convert.ToInt64(row["AudioFormatId"]), 
                DateAdded = TimeUtils.FromUnixTime(added),
                Modified = TimeUtils.FromUnixTime(modified),
                Created = TimeUtils.FromUnixTime(created),
                LastPlayed = lastPlayed!=null ? TimeUtils.FromUnixTime(lastPlayed.Value) : null,
                Comment = (string?)row["Comments"]??String.Empty,
                Rating = Database.GetValue<double>(row, "Rating"),
                PlayCount = Convert.ToInt32(row["PlayCount"]),
            };
            tracks.Add(track.DatabaseIndex, track);
        }

        return tracks;
    }

    public async Task UpdateGenresAsync(LibraryViewModel library)
    {
        try
        {
            string delete = $"DELETE FROM TrackGenres WHERE TrackId = {DatabaseIndex};";
            await library.Database.ExecuteNonQueryAsync(delete);
        
            string insert = "INSERT INTO TrackGenres (TrackId, GenreId) VALUES ($id, $genreId);";
            foreach (var genre in Genres.Select(x => x.DatabaseIndex))
            {
                _ = library.Database.ExecuteNonQueryAsync(insert, new()
                {
                    ["$id"] = DatabaseIndex,
                    ["$genreId"] = genre,
                });
            }
        }
        catch(Exception e){Console.WriteLine(e);}
    }

    public async Task UpdateArtistsAsync(LibraryViewModel library)
    {
        try
        {
            string delete = $"DELETE FROM TrackArtists WHERE TrackId = {DatabaseIndex};";
            await library.Database.ExecuteNonQueryAsync(delete);
            
            string insert=$"INSERT INTO TrackArtists (TrackId, ArtistId) VALUES ($id, $artistId);";
            foreach (var artist in Artists.Select(x => x.DatabaseIndex))
            {
                _ = library.Database.ExecuteNonQueryAsync(insert, new()
                {
                    ["$id"] = DatabaseIndex,
                    ["$artistId"] = artist
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