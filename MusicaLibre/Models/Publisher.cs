using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MusicaLibre.Services;
namespace MusicaLibre.Models;

public class Publisher:NameTag
{
    public Publisher(){}
    public Publisher(string Name)
    {
        this.Name = Name;
    }
 
    const string insertSql = @"
        INSERT INTO Publishers (Name, ArtworkId)
        VALUES ($name, $artworkId);
        SELECT last_insert_rowid();";
    private const string updateSql = @"
        UPDATE Publishers SET Name = $name, ArtworkId = $artworkId
        WHERE Id=$id";
    private const string deleteSql = "DELETE FROM Publishers WHERE Id=$id;";
    private const string selectSql = "SELECT * FROM Publishers;";
    
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
    
    public override async Task DbUpdateAsync(Database db)=> await db.ExecuteNonQueryAsync(updateSql, Parameters);
    public async Task DbDeleteAsync(Database db)=> await db.ExecuteNonQueryAsync(deleteSql, Parameters);
    
    public static Dictionary<long, Publisher> FromDatabase(Database db)
        => ProcessReaderQuery<Publisher>(db.ExecuteReader(selectSql));
    public static async Task<Dictionary<long, Publisher>> FromDatabaseAsync(Database db)
        => ProcessReaderQuery<Publisher>(await db.ExecuteReaderAsync(selectSql));
    
}