using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    const string insertSql = @"
        INSERT INTO Discs (AlbumId, Number) VALUES ($AlbumId, $number);
        SELECT last_insert_rowid();";

    private const string updateSql = @"
        UPDATE Discs SET
            Number = $number,
            AlbumId = $albumId
        WHERE Id = $id ";
    private const string deleteSql = @"
        DELETE FROM Discs
        WHERE Id = $id ";
    private Dictionary<string, object?> Parameters => new()
    {
        ["$id"] = DatabaseIndex,
        ["$AlbumId"] = AlbumId,
        ["$number"] = Number,
    };
    public void DbInsert(Database db)
    {
        var id = db.ExecuteScalar(insertSql, Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
    }

    public async Task DbInsertAsync(Database db, Action<long>? callback = null)
    {
        var id = await db.ExecuteScalarAsync(insertSql, Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
        callback?.Invoke(DatabaseIndex.Value);
    }
    
    public async Task DbUpdateAsync(Database db)
    {
        try
        {
            await db.ExecuteNonQueryAsync(updateSql, Parameters);
        }        
        catch(Exception e){Console.WriteLine(e);}
    }

    public async Task DbDeleteAsync(Database db)
    {
        try
        {
            await db.ExecuteNonQueryAsync(deleteSql, Parameters);
        }        
        catch(Exception e){Console.WriteLine(e);}
    }
    
    public static Dictionary<(uint, long), Disc> FromDatabase(Database db, int[]? indexes = null)
    {
        string filter = String.Empty;
        if (indexes != null && indexes.Length == 0)
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";

        string sql = $@" SELECT * FROM Discs {filter};";

        Dictionary<(uint, long), Disc> discs = new();
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
            discs.Add((disc.Number, disc.AlbumId), disc);
        }

        return discs;
    }

    public static Disc Null = new Disc(0, Album.Null)
    {
        DatabaseIndex = null
    };
}