using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MusicaLibre.Services;
namespace MusicaLibre.Models;

public class Genre:NameTag
{
    const string insertSql = @"
        INSERT INTO Genres (Name)
        VALUES ($name);
        SELECT last_insert_rowid();";
    
    const string updateSql =  @"
        UPDATE Genres SET 
            Name = $name 
            ArtworkId = $artworkId
        WHERE Id = $id;";

    const string removeSql = @"
        DELETE FROM Genres
        WHERE Id = $id;";
    
    public Genre(string name)
    {
        Name = name;
    }


    private Dictionary<string, object?> GetInsertParameters()=>new() { ["$name"] = Name, };
    public async Task DbInsertAsync(Database db, Action<long>? callback=null)
    {
        var id = await db.ExecuteScalarAsync(insertSql, GetInsertParameters());
        var index = Convert.ToInt64(id);
        DatabaseIndex = index;
        callback?.Invoke(index);
    }
    public void DbInsert(Database db)
    {
        var id = db.ExecuteScalar(insertSql, GetInsertParameters());
        DatabaseIndex =  Convert.ToInt64(id);
    }

    public Dictionary<string, object?> GetUpdateParameters() => new() {
        ["$name"] = Name,
        ["artworkId"] = Artwork?.DatabaseIndex != null ? Artwork.DatabaseIndex.Value : null, 
        ["id"] = DatabaseIndex, };
    public void DbUpdate(Database db)=> db.ExecuteNonQuery(updateSql, GetUpdateParameters());
    public async Task DbUpdateAsync(Database db)=>await db.ExecuteNonQueryAsync(updateSql, GetUpdateParameters());
    public Dictionary<string, object?> GetRemoveParameters() => new() {["$id"] = DatabaseIndex, };
    public void DbRemove(Database db)=>db.ExecuteNonQuery(removeSql, GetRemoveParameters());
    public async Task DbRemoveAsync(Database db)=>await db.ExecuteNonQueryAsync(removeSql, GetRemoveParameters());
    
    public static Dictionary<long, Genre> FromDatabase(Database db, int[]? indexes = null)
    {
        string filter = string.Empty;
        if (indexes != null && indexes.Length > 0)
        {
            // Build a parameterized query for all IDs
             
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";
        }

        string sql = $@" SELECT Id, Name FROM Genres {filter};";
        

        Dictionary<long, Genre> genres = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            var name = Database.GetString(row, "Name");
            Genre genre = new Genre(name!)
            {
                DatabaseIndex = Database.GetValue<long>(row, "Id")
            };
            genres.Add(genre.DatabaseIndex!.Value, genre);
        }

        return genres;
    }


    
    public static Genre Null = new Genre("Null")
    {
        DatabaseIndex = null
    };
}