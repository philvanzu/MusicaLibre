using System;
using System.Collections.Generic;
using MusicaLibre.Services;

namespace MusicaLibre.Models;

public class Year:NameTag
{
    public uint Number{get;set;}
    public Year(uint number)
    {
        Number = number;
        Name = number.ToString();
    }
    public void DbInsert(Database db)
    {
        const string sql = @"
        INSERT INTO Years (Number)
        VALUES ($number);
        SELECT last_insert_rowid();";

        var id = db.ExecuteScalar(sql, new()
        {
            ["$number"] = Number,
        });

        DatabaseIndex =  Convert.ToInt64(id);
    }
    
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
                DatabaseIndex = Database.GetValue<long>(row, "Id")
            };
            Years.Add(Year.DatabaseIndex!.Value, Year);
        }

        return Years;
    }
}