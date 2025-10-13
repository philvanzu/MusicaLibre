using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MusicaLibre.Services;

public static class Utils
{
    public static  T? Coalesce<T>(T[] values)
    {
        if (values == null || values.Length == 0)
            return default;

        // take first value as reference
        var first = values[0];

        // use EqualityComparer<T> to handle nulls & custom equality
        var comparer = EqualityComparer<T>.Default;

        for (int i = 1; i < values.Length; i++)
        {
            if (!comparer.Equals(first, values[i]))
                return default; // mismatch -> return null/default
        }

        return first;
    }
    
    public static string Escape(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;
        // Replace inner double quotes with single quotes
        return s.Replace("\"", "\\\"");
    }
    
    public static async Task CopyFileAsync(string source, string destination, bool overwrite = false)
    {
        if (!overwrite && File.Exists(destination))
            return;

        await using var src = File.OpenRead(source);
        await using var dst = File.Create(destination);
        await src.CopyToAsync(dst);
    }
}





public static class UnicodeUtils
{
    
    public static string asc = "⌃"; //⮝ ⮟ ‹›⥊ ⥋ ⥌⥎⥍ , ⥎ ↑↓  
    public static string desc = "⌄";
    public static string hamburger = "☰";
    public static string play = "▶";
}




