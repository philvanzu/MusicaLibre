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
    private Dictionary<string, object?> Parameters => new()
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
    
    public static Dictionary<long, Year> FromDatabase(Database db, int[]? indexes=null)
    {
        string filter = String.Empty;
        if (indexes != null && indexes.Length == 0)
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";

        string sql = $@"SELECT * FROM Years {filter}";
        

        Dictionary<long,Year> Years = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            var number = Database.GetValue<uint>(row, "Number");
            Year Year = new Year(number!.Value)
            {
                DatabaseIndex = Convert.ToInt64(row["Id"])
            };
            Years.Add(Year.DatabaseIndex, Year);
        }

        return Years;
    }
}