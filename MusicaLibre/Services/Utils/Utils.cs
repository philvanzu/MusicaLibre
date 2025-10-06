using System.Collections.Generic;

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

}





public static class UnicodeUtils
{
    
    public static string asc = "⌃"; //⮝ ⮟ ‹›⥊ ⥋ ⥌⥎⥍ , ⥎ ↑↓  
    public static string desc = "⌄";
    public static string hamburger = "☰";
    public static string play = "▶";
}




