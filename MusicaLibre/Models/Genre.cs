using System;
using System.Collections.Generic;
using System.Linq;
using MusicaLibre.Services;
namespace MusicaLibre.Models;

public class Genre:NameTag
{
    public Genre(string name)
    {
        Name = name;
    }
    
    public void DatabaseInsert(Database db)
    {
        const string sql = @"
        INSERT INTO Genres (Name)
        VALUES ($name);
        SELECT last_insert_rowid();";

        var id = db.ExecuteScalar(sql, new()
        {
            ["$name"] = Name,
        });

        DatabaseIndex =  Convert.ToInt64(id);
    }
    
    public static Dictionary<long, Genre> FromDatabase(Database db, int[]? indexes = null)
    {
        string filter = string.Empty;
        if (indexes != null && indexes.Length > 0)
        {
            // Build a parameterized query for all IDs
             
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";
        }

        string sql = $@" SELECT Id, Name FROM Genres {filter};";
        

        Dictionary<long, Genre> genres = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            var name = Database.GetString(row, "Name");
            Genre genre = new Genre(name!)
            {
                DatabaseIndex = Database.GetValue<long>(row, "Id")
            };
            genres.Add(genre.DatabaseIndex!.Value, genre);
        }

        return genres;
    }
    
    public static Genre Null = new Genre("Null")
    {
        DatabaseIndex = 0
    };
}