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

    private const string selectSql = "SELECT * FROM Folders;";
    
    public Folder() { }
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
    
    public static Dictionary<long, Folder> FromDatabase(Database db)
        =>ProcessReaderQuery<Folder>(db.ExecuteReader(selectSql));
    public static async Task<Dictionary<long, Folder>> FromDatabaseAsync(Database db)
        =>ProcessReaderQuery<Folder>(await db.ExecuteReaderAsync(selectSql));

    public override async Task DbUpdateAsync(Database db)
    {
        await db.ExecuteNonQueryAsync(updateSql, Parameters);
    }
    
    static Dictionary<long, Folder> ProcessReaderQuery(List<Dictionary<string, object?>> result)
    {
        Dictionary<long,Folder> folders = new();
        foreach (var row in result)
        {
            var name = Database.GetString(row, "Name");
            Folder folder = new Folder(name!)
            {
                DatabaseIndex = Convert.ToInt64(row["Id"]),
                ArtworkId = Convert.ToInt64(row["ArtworkId"]), 
                Name = name! 
            };
            folders.Add(folder.DatabaseIndex, folder);
        }

        return folders;
    }

}