namespace MusicaLibre.Models;

public class NameTag // artist, genre, publisher...
{
    public long? DatabaseIndex { get; set; }
    public string? Name { get; set; } 
    public Artwork? Artwork { get; set; }
}


