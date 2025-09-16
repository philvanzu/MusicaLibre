using System;
using System.Collections.Generic;
using MusicaLibre.Services;

namespace MusicaLibre.Models;

public abstract class NameTag // artist, genre, publisher...
{
    public long DatabaseIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? ArtworkId { get; set; }
    public Artwork? Artwork { get; set; }
    
    protected virtual Dictionary<string, object?> Parameters => new()
    {
        ["$name"] = Name,
        ["$artworkId"] = Artwork?.DatabaseIndex,
        ["$id"] = DatabaseIndex,
    };
    
    protected static Dictionary<long, T> ProcessReaderQuery<T>(List<Dictionary<string, object?>> result) where T : NameTag, new()
    {
        Dictionary<long,T> items = new();
        foreach (var row in result)
        {
            T item = new T()
            {
                DatabaseIndex = Convert.ToInt64(row["Id"]),
                ArtworkId = Database.GetValue<long>(row, "ArtworkId"), 
                Name = (string?) row["Name"] ?? string.Empty, 
            };
            items.Add(item.DatabaseIndex, item);
        }

        return items;
    }

}


