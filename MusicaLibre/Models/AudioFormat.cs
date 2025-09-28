using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using MusicaLibre.Services;

namespace MusicaLibre.Models;

public class AudioFormat:NameTag
{
    public AudioFormat(){}
    public AudioFormat(string name)
    {
        Name = name;
    }
    private const string insertSql = @"
                    INSERT INTO AudioFormats (Name) VALUES ($name);
                    SELECT last_insert_rowid();";
    
    private const string selectSql="SELECT * FROM AudioFormats;";
    
    private const string updateSql = @"
        UPDATE AudioFormats SET Name = $name, ArtworkId = $artworkId
        WHERE Id = $id;";
    
    private const string deleteSql=@"
        DELETE FROM AudioFormats WHERE Id = $id;";

    public static string Generate(string description, int bitrate)
    {
        string codec = NormalizeCodec(description);
        string density = GetDensity(codec, description, bitrate);

        return $"{codec}{(string.IsNullOrEmpty(density) ? "" : $" ({density})")}";
    }

    public void DbInsert(Database db)
    {
        var id = db.ExecuteScalar(insertSql, Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
    }

    public async Task DbInsertAsync(Database db)
    {
        var id = await db.ExecuteScalarAsync(insertSql, Parameters);
        DatabaseIndex =  Convert.ToInt64(id);
    }

    public override async Task DbUpdateAsync(Database db)
    {
        await db.ExecuteNonQueryAsync(updateSql, Parameters);
    }

    public static Dictionary<long, AudioFormat> FromDatabase(Database db)
        => ProcessReaderQuery<AudioFormat>(db.ExecuteReader(selectSql));
    public static async Task<Dictionary<long, AudioFormat>> FromDatabaseAsync(Database db)
        => ProcessReaderQuery<AudioFormat>(await db.ExecuteReaderAsync(selectSql));
    
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


}

