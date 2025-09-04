using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MusicaLibre.Services;

public class GenreManager
{
    public static readonly Dictionary<string, string> canonicalMap = new (StringComparer.OrdinalIgnoreCase)
    {
        { "Hip Hop", "Hip-Hop" },
        { "RnB", "R&B" },
        { "R & b", "R&B" },
        { "DnB", "Drum & Bass" },
        { "D & B", "Drum & Bass" },
        { "Drum 'n Bass", "Drum & Bass" },
        { "Drum 'n' Bass", "Drum & Bass" },
        { "Dub Techno", "Dub-Techno" },
        { "Dub.Techno", "Dub-Techno" },
        { "Alt-Rock", "Alternative Rock" },
        { "Alternative-Rock", "Alternative Rock" },
        { "Alt. Rock", "Alternative Rock" },
        { "AlternRock", "Alternative Rock" },
        { "Dreampop", "Dream Pop" },
        { "Synthpop", "Synth-Pop" },
        { "Synthwave", "Synth-Wave" },
        { "Punkrock", "Punk Rock" },
        { "Punk-Rock", "Punk Rock" },
        { "Punk.Rock", "Punk Rock" },
        { "Shoegazing", "Shoegaze" },
        { "Trip Hop", "Trip-Hop" },
    };
    public static readonly Dictionary<string, string> prefixMap = new (StringComparer.Ordinal)
    {
        { "Alt ", "Alt-"},
        { "Altern ", "Alt-"},
        { "Indie ", "Indie-"},
        { "Neo ", "Neo-" },
        { "Nu ", "Nu-" },
        { "New ", "New-" },
        { "Hard ", "Hard-"},
        { "Post ", "Post-" },
        { "Pre ", "Pre-" },
        { "Deep ", "Deep-" },
        { "Synth ", "Synth-" },
        { "Dark ", "Dark-"},
        { "Electro ", "Electro-"},
        { "Tech ", "Tech-"},
        { "Euro ", "Euro-"},
        { "J ", "J-"},
        { "K ", "K-"},
    };

    public static readonly Dictionary<string, string> acronymsMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Idm", "IDM" },
        {"Ost", "OST"},
        {"Uk", "UK"},
        {"Edm","EDM"}
    };
    private static GenreManager? _instance;

    public static GenreManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Load();
            }
            return _instance;
        }
    }
    public List<string> Genres { get; set; } = new();
    public Dictionary<string, string> Mappings { get; set; } = new();

    public string? GetOrMapGenreAsync(string rawGenre)
    {
        var normalized = Normalize(rawGenre);
        if (Mappings.TryGetValue(normalized, out var curated))
            return curated;

        return null;
    }

    private string Normalize(string s) => s.Trim().ToLowerInvariant();
    private static GenreManager Load()
    {
        //deserialize from DB or app data
        return new GenreManager();
    }

    public void Save()
    {
        //serialize to db or app data
    }

}