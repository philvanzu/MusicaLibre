using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MusicaLibre.Services;
namespace MusicaLibre.Models;

public class Publisher:NameTag
{
    public Publisher(string Name)
    {
        this.Name = Name;
    }
 
    const string insertSql = @"
        INSERT INTO Publishers (Name)
        VALUES ($name);
        SELECT last_insert_rowid();";

    private const string updateSql = @"
        UPDATE Publishers SET Name = $name, ArtworkId = $artworkId
        WHERE Id=$id";
    
    private const string deleteSql = @"
        DELETE FROM Publishers WHERE Id=$id;";
    
    private Dictionary<string, object?> Parameters => new()
    {
        ["$name"] = Name,
        ["$artworkId"] = Artwork?.DatabaseIndex,
        ["$id"]= DatabaseIndex,
    };
    
    public void DbInsert(Database db)
    {
        var id = db.ExecuteScalar(insertSql, Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
    }
    public async Task DbInsertAsync(Database db)
    {
        var id = await  db.ExecuteScalarAsync(insertSql, Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
    }
    
    public async Task DbUpdateAsync(Database db)=> await db.ExecuteNonQueryAsync(updateSql, Parameters);
    public async Task DbDeleteAsync(Database db)=> await db.ExecuteNonQueryAsync(deleteSql, Parameters);
    
    public static Dictionary<long, Publisher> FromDatabase(Database db, int[]? indexes = null)
    {
        string filter = String.Empty;
        if (indexes != null && indexes.Length == 0)
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";

        string sql = $@" SELECT * FROM Publishers {filter};";

        Dictionary<long, Publisher> publishers = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            var name = Database.GetString(row, "Name");
            Publisher publisher = new Publisher(name!)
            {
                DatabaseIndex = Convert.ToInt64(row["Id"]),
            };
            publishers.Add(publisher.DatabaseIndex, publisher);
        }

        return publishers;
    }
}