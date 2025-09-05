using System;
using System.Collections.Generic;
using System.Linq;
using MusicaLibre.Services;

namespace MusicaLibre.Models;

public class Artist:NameTag
{
    public Artist(string name)
    {
        Name = name;
    }

    

    public void DatabaseInsert(Database db)
    {
        const string sql = @"
        INSERT INTO Artists (Name)
        VALUES ($name);
        SELECT last_insert_rowid();";

        var id = db.ExecuteScalar(sql, new()
        {
            ["$name"] = Name,
        });

        DatabaseIndex =  Convert.ToInt64(id);
    }
    
    public static Dictionary<long, Artist> FromDatabase(Database db, int[]? indexes=null)
    {
        string filter = String.Empty;
        if (indexes != null && indexes.Length == 0)
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";

        string sql = $@"SELECT Id, Name FROM Artists {filter}";
        

        Dictionary<long,Artist> artists = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            var name = Database.GetString(row, "Name");
            Artist artist = new Artist(name!)
            {
                DatabaseIndex = Database.GetValue<long>(row, "Id")
            };
            artists.Add(artist.DatabaseIndex!.Value, artist);
        }

        return artists;
    }
    public static Artist Null = new Artist("Null")
    {
        DatabaseIndex = 0
    };
}