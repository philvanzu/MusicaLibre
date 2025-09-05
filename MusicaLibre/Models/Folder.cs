using System;
using System.Collections.Generic;
using MusicaLibre.Services;

namespace MusicaLibre.Models;

public class Folder:NameTag
{
    public Folder(string name)
    {
        Name = name;
    }
    
    public void DatabaseInsert(Database db)
    {
        const string sql = @"
        INSERT INTO Folders (Name)
        VALUES ($name);
        SELECT last_insert_rowid();";

        var id = db.ExecuteScalar(sql, new()
        {
            ["$name"] = Name,
        });

        DatabaseIndex =  Convert.ToInt64(id);
    }
    
    public static Dictionary<long, Folder> FromDatabase(Database db, int[]? indexes = null)
    {
        string filter = string.Empty;
        if (indexes != null && indexes.Length > 0)
        {
            // Build a parameterized query for all IDs
             
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";
        }

        string sql = $@" SELECT * FROM Folders {filter};";
        

        Dictionary<long, Folder> Folders = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            var name = Database.GetString(row, "Name");
            Folder Folder = new Folder(name!)
            {
                DatabaseIndex = Database.GetValue<long>(row, "Id")
            };
            Folders.Add(Folder.DatabaseIndex!.Value, Folder);
        }

        return Folders;
    }
    public static Folder Null = new Folder("Null")
    {
        DatabaseIndex = 0
    };
}