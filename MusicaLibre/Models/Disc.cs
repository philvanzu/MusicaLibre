using System;
using System.Collections.Generic;
using MusicaLibre.Services;

namespace MusicaLibre.Models;

public class Disc
{
    public long? DatabaseIndex { get; set; }
    public Album Album { get; set; }
    public long AlbumId { get; set; }
    public uint Number { get; set; }
    public string? Name { get; set; }
    public Artwork? Artwork { get; set; }
    public long? ArtworkId { get; set; }
    
    public Disc(uint number, long albumId)
    {
        Number = number;
        AlbumId = albumId;
    }

    public Disc(uint number, Album album)
    {
        Number = number;
        if(!album.DatabaseIndex.HasValue)throw new ("Cannot create album disc without an album database index");
        AlbumId = album.DatabaseIndex.Value;
    }
    
    public void DatabaseInsert(Database db)
    {
        const string sql = @"
        INSERT INTO Discs (AlbumId, Number) VALUES ($AlbumId, $number);
        SELECT last_insert_rowid();";

        var id = db.ExecuteScalar(sql, new()
        {
            ["$AlbumId"] = AlbumId,
            ["$number"] = Number,
        });

        DatabaseIndex =  Convert.ToInt64(id);
    }
    
    public static Dictionary<long, Disc> FromDatabase(Database db, int[]? indexes = null)
    {
        string filter = String.Empty;
        if (indexes != null && indexes.Length == 0)
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";

        string sql = $@" SELECT * FROM Discs {filter};";

        Dictionary<long, Disc> discs = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            var number = Convert.ToUInt32(row["Number"]);
            var albumId = Convert.ToInt64(row["AlbumId"]);
            
            Disc disc = new Disc(number, albumId)
            {
                DatabaseIndex = Convert.ToInt64(row["Id"]),
                Name = Database.GetString(row, "Name"),
                ArtworkId = Database.GetValue<long>(row, "ArtworkId"),
            };
            discs.Add(disc.DatabaseIndex.Value, disc);
        }

        return discs;
    }

    public static Disc Null = new Disc(0, Album.Null)
    {
        DatabaseIndex = 0
    };
}