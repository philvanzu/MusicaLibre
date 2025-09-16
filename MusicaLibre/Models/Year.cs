using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MusicaLibre.Services;

namespace MusicaLibre.Models;

public class Year:NameTag
{
    const string insertSql = @"
        INSERT INTO Years (Number)
        VALUES ($number);
        SELECT last_insert_rowid();";

    private const string updateSql = @"
        UPDATE Years SET Number = $number, ArtworkId = $artworkId
        WHERE Id = $id;";
    private const string deleteSql = @"
        DELETE FROM Years WHERE Id = $id;";
    const string selectSql="SELECT * FROM Years;";
    protected override Dictionary<string, object?> Parameters => new()
    {
        ["$id"] = DatabaseIndex,
        ["$number"] = Number,
        ["$artworkId"] = Artwork?.DatabaseIndex
    };
    public uint Number{get;set;}
    
    public Year(uint number)
    {
        Number = number;
        Name = number.ToString();
    }
    public void DbInsert(Database db)
    {
        var id = db.ExecuteScalar(insertSql, Parameters );
        DatabaseIndex =  Convert.ToInt64(id);
    }
    public async Task DbInsertAsync(Database db, Action<long>? callback = null)
    {
        var id = await db.ExecuteScalarAsync(updateSql, Parameters );
        DatabaseIndex =  Convert.ToInt64(id);
        callback?.Invoke(Convert.ToInt64(id));
    }
    public async Task DbUpdateAsync(Database db)=> await db.ExecuteNonQueryAsync(updateSql, Parameters );
    public async Task DbDeleteAsync(Database db)=> await db.ExecuteNonQueryAsync(deleteSql, Parameters );
    
    public static Dictionary<long, Year> FromDatabase(Database db)
        =>ProcessReaderQuery(db.ExecuteReader(selectSql));
    public static async Task<Dictionary<long, Year>> FromDatabaseAsync(Database db)
        => ProcessReaderQuery(await db.ExecuteReaderAsync(selectSql));
    protected static Dictionary<long, Year> ProcessReaderQuery(List<Dictionary<string, object?>> result) 
    {
        Dictionary<long,Year> ts = new();
        foreach (var row in result)
        {
            var number = Convert.ToUInt32(row["Number"]);
            Year t = new Year(number)
            {
                DatabaseIndex = Convert.ToInt64(row["Id"]),
                ArtworkId = Database.GetValue<long>(row, "ArtworkId"), 
            };
            ts.Add(t.DatabaseIndex, t);
        }

        return ts;
    }
}