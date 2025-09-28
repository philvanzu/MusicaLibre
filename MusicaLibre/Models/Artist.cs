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
    
    private const string selectSql = $@"SELECT * FROM Artists";
    
    public Artist() { }

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
        var id = await db.ExecuteScalarAsync(insertSql, Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
        callback?.Invoke(DatabaseIndex);
    }
    public override async Task DbUpdateAsync(Database db)=>await db.ExecuteNonQueryAsync(updateSql, Parameters);
    public async Task DbDeleteAsync(Database db) => await db.ExecuteNonQueryAsync(deleteSql, Parameters);
    
    public static Dictionary<long, Artist> FromDatabase(Database db)
        =>ProcessReaderQuery<Artist>(db.ExecuteReader(selectSql));
    
    public static async Task<Dictionary<long, Artist>> FromDatabaseAsync(Database db)
        => ProcessReaderQuery<Artist>(await db.ExecuteReaderAsync(selectSql));
  

}