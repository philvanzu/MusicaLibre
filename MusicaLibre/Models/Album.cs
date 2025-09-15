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
    public Year? Year { get; set; }
    public long? YearId { get; set; }
    public Folder Folder { get; set; }
    public long FolderId { get; set; }
    public DateTime? Modified { get; set; }
    public DateTime? Created { get; set; }
    public DateTime? LastPlayed { get; set; }
    public DateTime? Added { get; set; }
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
            return this.Title.Equals(album.Title, StringComparison.Ordinal)
                   && this.AlbumArtist.Name.Equals(album.AlbumArtist.Name, StringComparison.Ordinal)
                   && this.Year.Equals(album.Year);    
        }
        return false;
    }
    
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
        ["$year"] = Year?.DatabaseIndex,
        ["$rootfolder"] = Folder?.DatabaseIndex,
        ["$albumartist"] = AlbumArtist?.DatabaseIndex,
        ["$cover"] = Cover?.DatabaseIndex,
        ["$modified"] = Modified.HasValue ? TimeUtils.ToUnixTime(Modified!.Value) : null,
        ["$created"] = Created.HasValue ? TimeUtils.ToUnixTime(Created!.Value) : null,
        ["$lastplayed"] = LastPlayed.HasValue ? TimeUtils.ToUnixTime(LastPlayed!.Value) : null,
        ["$added"] = Added.HasValue ? TimeUtils.ToUnixTime(Added!.Value) : null,
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
    public static Dictionary<long, Album> FromDatabase(Database db, int[]? indexes=null)
    {
        string filter = String.Empty;
        if (indexes != null && indexes.Length == 0)
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";

        string sql = $@"SELECT * FROM Albums  {filter};";

        Dictionary<long, Album> albums = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            
            var modified = Database.GetValue<long>(row, "Modified");
            var added = Database.GetValue<long>(row, "Added");
            var lastPlayed = Database.GetValue<long>(row, "LastPlayed");
            var created = Database.GetValue<long>(row, "Created");

            Album album = new Album()
            {
                DatabaseIndex = Convert.ToInt64(row["Id"]),
                Title = (string?)row["Title"] ?? string.Empty,
                ArtistId = Convert.ToInt64(row["AlbumArtist"]),
                YearId = Database.GetValue<long>(row, "YearId"),
                FolderId = Convert.ToInt64(row["FolderId"]),
                Added = added!=null ? TimeUtils.FromUnixTime(added.Value) : null,
                Modified = modified!=null ? TimeUtils.FromUnixTime(modified.Value) : null,
                Created = created!=null ? TimeUtils.FromUnixTime(created.Value) : null,
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
        
        var rootf = library.Folders.Values.FirstOrDefault(x => x.Name == rootFolder);
        if (rootf is null)
        {
            rootf = new Folder(rootFolder);
            await rootf.DbInsertAsync(library.Database);
            library.Folders.Add(rootf.DatabaseIndex, rootf);
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