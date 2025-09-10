using System;
using System.Collections.Generic;
using System.ComponentModel;
using MusicaLibre.Services;

namespace MusicaLibre.Models;

public class AudioFormat:NameTag
{
    public AudioFormat(string name)
    {
        Name = name;
    }
    public static string Generate(string description, int bitrate)
    {
        string codec = NormalizeCodec(description);
        string density = GetDensity(codec, description, bitrate);

        return $"{codec}{(string.IsNullOrEmpty(density) ? "" : $" ({density})")}";
    }

    public void DbInsert(Database db)
    {
        var sql = @"INSERT INTO AudioFormats (Name) VALUES ($name);
                    SELECT last_insert_rowid();";

        var id = db.ExecuteScalar(sql, new()
        {
            ["$name"] = Name
        });
        DatabaseIndex =  Convert.ToInt64(id);
    }

    public static Dictionary<long, AudioFormat> FromDatabase(Database db, int[]? indexes = null)
    {
        string filter = string.Empty;
        if (indexes != null && indexes.Length > 0)
        {
            // Build a parameterized query for all IDs
             
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";
        }
        var sql  = $"SELECT * FROM AudioFormats {filter};";
        Dictionary<long, AudioFormat> formats = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            var name = Database.GetString(row, "Name");
            var id = Database.GetValue<long>(row, "Id");
            var format = new AudioFormat(name)
            {
                DatabaseIndex = id,
            };
            formats.Add(id.Value, format);
        }
        return formats;
    }
    private static string NormalizeCodec(string description)
    {
        description = description.ToLowerInvariant();

        if (description.Contains("flac"))
            return "FLAC";
        if (description.Contains("alac") || description.Contains("apple lossless"))
            return "ALAC";
        if (description.Contains("mpeg") && description.Contains("layer 3"))
            return "MP3";
        if (description.Contains("aac"))
            return "AAC";
        if (description.Contains("vorbis"))
            return "Vorbis";
        if (description.Contains("opus"))
            return "Opus";
        if (description.Contains("monkey"))
            return "APE";
        if (description.Contains("wavpack") || description.Contains("wv"))
            return "WavPack";
        if (description.Contains("musepack") || description.Contains("mpc"))
            return "Musepack";
        if (description.Contains("wma"))
            return "WMA";

        return description; // fallback
    }

    private static string GetDensity(string codec, string description, int bitrate)
    {
        // Lossless: don't bin by bitrate
        if (codec is "FLAC" or "ALAC" or "APE" or "WavPack")
        {
            return bitrate switch
            {
                < 500 => "Low bitrate",
                < 1000 => "Medium bitrate",
                < 2000 => "High bitrate",
                < 3000 => "Very High bitrate",
                _ => "Ultra High bitrate"
            };
        }

        if (codec == "MP3" || codec == "AAC" || codec == "Vorbis" || codec == "WMA")
        {
            if(description.Contains("vbr", StringComparison.OrdinalIgnoreCase))
                return "VBR";
            
            return bitrate switch
            {
                < 128 => "Low bitrate",
                128 => "128Kbps",
                < 192 => "Medium bitrate",
                192 => "192Kbps",
                < 256 => "High bitrate",
                256 => "256Kbps",
                < 320 => "Very High bitrate",
                320 => "320Kbps",
                _ => "Unknown"
            };
        }
        return string.Empty;
    }

    public static AudioFormat Null = new AudioFormat("Null")
    {
        DatabaseIndex = null
    };
}

