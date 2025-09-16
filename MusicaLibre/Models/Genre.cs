using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MusicaLibre.Services;
using TagLib;

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

    const string deleteSql = @"
        DELETE FROM Genres
        WHERE Id = $id;";
    
    const string selectSql = "SELECT * FROM Genres;";

    public Genre() { }

    public Genre(string name)
    {
        Name = name;
    }


    public async Task DbInsertAsync(Database db, Action<long>? callback=null)
    {
        var id = await db.ExecuteScalarAsync(insertSql, Parameters);
        var index = Convert.ToInt64(id);
        DatabaseIndex = index;
        callback?.Invoke(index);
    }
    public void DbInsert(Database db)
    {
        var id = db.ExecuteScalar(insertSql, Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
    }

    public void DbUpdate(Database db)=> db.ExecuteNonQuery(updateSql, Parameters);
    public async Task DbUpdateAsync(Database db)=>await db.ExecuteNonQueryAsync(updateSql, Parameters);
    public void DbRemove(Database db)=>db.ExecuteNonQuery(deleteSql, Parameters);
    public async Task DbRemoveAsync(Database db)=>await db.ExecuteNonQueryAsync(deleteSql, Parameters);
    
    public static Dictionary<long, Genre> FromDatabase(Database db)
        =>ProcessReaderQuery<Genre>(db.ExecuteReader(selectSql));
    public static async Task<Dictionary<long, Genre>> FromDatabaseAsync(Database db)
        =>ProcessReaderQuery<Genre>(await db.ExecuteReaderAsync(selectSql));
    
}