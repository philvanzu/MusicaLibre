using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MusicaLibre.Services;

namespace MusicaLibre.Models;

public class Artist:NameTag
{
    const string insertSql = @"
        INSERT INTO Artists (Name)
        VALUES ($name);
        SELECT last_insert_rowid();";

    private const string updateSql = @"
        UPDATE Artists SET Name = $name, ArtworkId = $artworkId
        WHERE Id = $id;";
    
    private const string deleteSql=@"
        DELETE FROM Artists WHERE Id = $id;";
    
    private Dictionary<string, object?> Parameters => new()
    {
        ["$name"] = Name,
        ["$artworkId"] = Artwork?.DatabaseIndex,
        ["$id"] = DatabaseIndex,
    };
    public Artist(string name)
    {
        Name = name;
    }

    public void DbInsert(Database db)
    {
        var id = db.ExecuteScalar(insertSql, Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
    }
    public async Task DbInsertAsync(Database db, Action<long>? callback=null)
    {
        var id = await db.ExecuteScalarAsync(updateSql, Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
        callback?.Invoke(DatabaseIndex.Value);
    }
    public async Task DbUpdateAsync(Database db)=>await db.ExecuteNonQueryAsync(updateSql, Parameters);
    public async Task DbDeleteAsync(Database db) => await db.ExecuteNonQueryAsync(deleteSql, Parameters);
    
    public static Dictionary<long, Artist> FromDatabase(Database db, int[]? indexes=null)
    {
        string filter = String.Empty;
        if (indexes != null && indexes.Length == 0)
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";

        string sql = $@"SELECT Id, Name FROM Artists {filter}";
        

        Dictionary<long,Artist> artists = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            var name = Database.GetString(row, "Name");
            Artist artist = new Artist(name!)
            {
                DatabaseIndex = Database.GetValue<long>(row, "Id")
            };
            artists.Add(artist.DatabaseIndex!.Value, artist);
        }

        return artists;
    }
    
    public static Artist Null = new Artist("Null")
    {
        DatabaseIndex = null
    };
}