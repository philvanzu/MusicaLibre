using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Models;

public class Album
{
    public long DatabaseIndex { get; set; }
    public string Title { get; set; }
    public Artist AlbumArtist { get; set; }
    public long ArtistId { get; set; }
    public Artwork? Cover { get; set; }
    public long? CoverId { get; set; }
    public Year Year { get; set; }
    public long YearId { get; set; }
    public Folder Folder { get; set; }
    public long FolderId { get; set; }
    public DateTime Modified { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastPlayed { get; set; }
    public DateTime Added { get; set; }
    public HashSet<Artwork> Artworks { get; set; }=new HashSet<Artwork>();
    public List<long> ArtworkIds { get; set; }=new List<long>();

    public Album(string title, Artist albumArtist, Year year)
    {
        Title = title;
        AlbumArtist = albumArtist;
        Year = year;
    }

    public Album()
    {
        
    }
    public void AddArtwork(Artwork artwork)
    {
        Artworks.Add(artwork);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Title, AlbumArtist, Year);
    }
    public override bool Equals(object? obj)
    {
        if (obj is Album album)
        {
            return Title.Equals(album.Title, StringComparison.Ordinal)
                   && AlbumArtist.Name.Equals(album.AlbumArtist.Name, StringComparison.Ordinal)
                   && Year.Equals(album.Year);    
        }
        return false;
    }

    private const string selectSql = "SELECT * FROM Albums;";
    
    const string insertSql = @"
            INSERT INTO Albums (Title, YearId, FolderId, AlbumArtist, CoverId, Modified, Created, LastPlayed, Added)
            VALUES ($title, $year, $rootfolder, $albumartist, $cover, $modified, $created, $lastplayed, $added);
            SELECT last_insert_rowid();";
    const string updateSql = @"
        UPDATE Albums SET 
            Title = $title, 
            YearId = $year, 
            FolderId = $rootfolder,
            AlbumArtist = $albumartist, 
            CoverId = $cover, 
            Modified = $modified, 
            Created = $created, 
            LastPlayed = $lastplayed, 
            Added = $added
        WHERE Id=$id;";
    const string deleteSql = @"DELETE FROM Albums WHERE Id=$id;";

    public Dictionary<string, object?> Parameters => new()
    {
        ["$id"] = DatabaseIndex,
        ["$title"] = Title,
        ["$year"] = Year.DatabaseIndex,
        ["$rootfolder"] = Folder.DatabaseIndex,
        ["$albumartist"] = AlbumArtist.DatabaseIndex,
        ["$cover"] = Cover?.DatabaseIndex,
        ["$modified"] = TimeUtils.ToUnixTime(Modified),
        ["$created"] = TimeUtils.ToUnixTime(Created),
        ["$lastplayed"] = LastPlayed.HasValue ? TimeUtils.ToUnixTime(LastPlayed!.Value) : null,
        ["$added"] = TimeUtils.ToUnixTime(Added),
    };
    public void DbInsert(Database db)
    {
        var id = db.ExecuteScalar(insertSql, Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
    }

    public async Task DbInsertAsync(Database db, Action<long> callback)
    {
        try
        {
            var id = await db.ExecuteScalarAsync(insertSql, Parameters);
            DatabaseIndex =  Convert.ToInt64(id);
            callback(Convert.ToInt64(id));    
        }
        catch (Exception ex){Console.WriteLine(ex);}
    }
    public void DbUpdate(Database db)=>db.ExecuteNonQuery(updateSql, Parameters);
    public async Task DbUpdateAsync(Database db)
    {
        try { await db.ExecuteNonQueryAsync(updateSql, Parameters); }
        catch (Exception ex) {Console.WriteLine(ex);}
    }
    public void DbDelete(Database db)=>db.ExecuteNonQuery(deleteSql, Parameters);
    public async Task DbDeleteAsync(Database db)
    {
        try { await db.ExecuteNonQueryAsync(deleteSql, Parameters); }
        catch (Exception ex) {Console.WriteLine(ex);}
    }

    public static Dictionary<long, Album> FromDatabase(Database db) 
        => ProcessReaderResult(db.ExecuteReader(selectSql));
    
    public static async Task<Dictionary<long, Album>> FromDatabaseAsync(Database db) 
        => ProcessReaderResult(await db.ExecuteReaderAsync(selectSql));

    static Dictionary<long, Album> ProcessReaderResult(List<Dictionary<string, object?>> result)
    {
        Dictionary<long, Album> albums = new();
        foreach (var row in result)
        {
            
            var modified = Convert.ToInt64(row["Modified"]);
            var added =  Convert.ToInt64(row["Added"]);
            var lastPlayed = Database.GetValue<long>(row, "LastPlayed");
            var created =  Convert.ToInt64(row["Created"]);

            Album album = new Album()
            {
                DatabaseIndex = Convert.ToInt64(row["Id"]),
                Title = (string?)row["Title"] ?? string.Empty,
                ArtistId = Convert.ToInt64(row["AlbumArtist"]),
                YearId = Convert.ToInt64(row["YearId"]),
                FolderId = Convert.ToInt64(row["FolderId"]),
                Added = TimeUtils.FromUnixTime(added) ,
                Modified = TimeUtils.FromUnixTime(modified) ,
                Created = TimeUtils.FromUnixTime(created) ,
                LastPlayed = lastPlayed!=null ? TimeUtils.FromUnixTime(lastPlayed.Value) : null,
                CoverId = Database.GetValue<long>(row, "CoverId"),
            };
            albums.Add(album.DatabaseIndex, album);
        }

        return albums;
    }  

    public async Task ComputeRootFolder(LibraryViewModel library, List<Track> tracks)
    {
        var folders = tracks.Select(x => x.Folder.Name).Distinct().ToList();
        var rootFolder = (folders.Count > 1) ? PathUtils.GetCommonRoot(folders) : folders.FirstOrDefault();
        if (rootFolder is null  || !PathUtils.IsDescendantPath(library.Path, rootFolder))
            rootFolder = folders.FirstOrDefault();
        
        if (string.IsNullOrEmpty(rootFolder))
            throw new Exception("Root folder not found");
        
        var rootf = library.Data.Folders.Values.FirstOrDefault(x => x.Name == rootFolder);
        if (rootf is null)
        {
            rootf = new Folder(rootFolder);
            await rootf.DbInsertAsync(library.Database);
            library.Data.Folders.Add(rootf.DatabaseIndex, rootf);
        }
        Folder = rootf;
        await DbUpdateAsync(library.Database);
    }
    
    public void FindAlbumCover()
    {
        if (Artworks.Count == 0) return;
        else if (Artworks.Count == 1)
        {
            Cover = Artworks.First();
            return;
        }
        foreach (var artwork in Artworks)
        {
            if (artwork.Role == ArtworkRole.CoverFront )
            {
                Cover = artwork;
                return;
            }

            if (artwork.Role == ArtworkRole.Other)
            {
                var filename = System.IO.Path.GetFileNameWithoutExtension(artwork.SourcePath)??string.Empty;
                var escapedTitle = Regex.Escape(Title);
                var pattern = Regex.Replace(escapedTitle, @"\s+", @"\W+"); 
                bool match = Regex.IsMatch(filename, pattern, RegexOptions.IgnoreCase);

                if ( match)
                {
                    Cover = artwork;
                    return;
                }
            }
            
        }

        Cover = Artworks.First();
    }
}