using System;
using System.Collections.Generic;
using System.Linq;
using MusicaLibre.Services;
namespace MusicaLibre.Models;

public class Publisher:NameTag
{
    public Publisher(string Name)
    {
        this.Name = Name;
    }
    
    public void DatabaseInsert(Database db)
    {
        const string sql = @"
        INSERT INTO Publishers (Name)
        VALUES ($name);
        SELECT last_insert_rowid();";

        var id = db.ExecuteScalar(sql, new()
        {
            ["$name"] = Name,
        });

        DatabaseIndex =  Convert.ToInt64(id);
    }
    
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
                DatabaseIndex = Database.GetValue<long>(row, "Id")
            };
            publishers.Add(publisher.DatabaseIndex!.Value, publisher);
        }

        return publishers;
    }

    public static Publisher Null = new Publisher("Null")
    {
        DatabaseIndex = null
    };
}