using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MusicaLibre.Services;

namespace MusicaLibre.Models;

public class Folder:NameTag
{
    const string insertSql = @"
        INSERT INTO Folders (Name)
        VALUES ($name);
        SELECT last_insert_rowid();";

    private const string updateSql = @"
        UPDATE Folders SET Name = $name, ArtworkId = $artworkId
        WHERE Id = $id;";
    
    private const string deleteSql = @"
        DELETE FROM Folders WHERE Id = $id;";

    private Dictionary<string, object?> Parameters => new(){
        ["$name"] = Name,
        ["$artworkId"] = Artwork?.DatabaseIndex,
        ["$id"] = DatabaseIndex,
    };
    
    public Folder(string name)
    {
        Name = name;
    }
    
    public void DbInsert(Database db)
    {
        var id = db.ExecuteScalar(insertSql,Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
    }
    public async Task DbInsertAsync(Database db, Action<long>? callback=null)
    {
        var id = await db.ExecuteScalarAsync(insertSql,Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
        callback?.Invoke(Convert.ToInt64(id));
    }
    public void DbDelete(Database db) => db.ExecuteNonQuery(deleteSql,Parameters);
    public async Task DbDeleteAsync(Database db)=> await db.ExecuteNonQueryAsync(deleteSql,Parameters);
    
    public static Dictionary<long, Folder> FromDatabase(Database db, int[]? indexes = null)
    {
        string filter = string.Empty;
        if (indexes != null && indexes.Length > 0)
        {
            // Build a parameterized query for all IDs
             
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";
        }

        string sql = $@" SELECT * FROM Folders {filter};";
        

        Dictionary<long, Folder> Folders = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            var name = Database.GetString(row, "Name");
            Folder Folder = new Folder(name!)
            {
                DatabaseIndex = Convert.ToInt64(row["Id"])
            };
            Folders.Add(Folder.DatabaseIndex, Folder);
        }

        return Folders;
    }
}