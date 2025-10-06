using System;
using System.Collections.Generic;
using System.Globalization;

namespace MusicaLibre.Services;

public static class TimeUtils
{
    public static long ToUnixTime(DateTime dt) =>
        ((DateTimeOffset)dt.ToUniversalTime()).ToUnixTimeSeconds();

    public static DateTime FromUnixTime(long ts) =>
        DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;
    
    public static double ToMilliseconds(TimeSpan ts)=>ts.TotalMilliseconds;
    public static TimeSpan FromMilliseconds(double ts) => TimeSpan.FromMilliseconds(ts);
    
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return duration.ToString(@"hh\:mm\:ss");   // 1:23:45
        else
            return duration.ToString(@"m\:ss");       // 4:05
    }

    public static string FormatDate(DateTime? dt)
    {
        return dt?.ToString("yyyy-MM-dd") ?? string.Empty;
    }
    public static DateTime? FromDateString(string? s)
    {
        if (DateTime.TryParseExact(
                s,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
            return parsed;
        
        return null;
    }
    public static string FormatDateTime(DateTime? dt)
    {
        return dt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
    }

    public static DateTime? FromDateTimeString(string? s)
    {
        if (DateTime.TryParseExact(
                s,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
            return parsed;
        
        return null;
    }

    public static DateTime Earliest(IEnumerable<DateTime> array)
    {
        var ret = DateTime.MaxValue;
        foreach (var date in array)
            if (date !=null && date < ret)
                ret = date;
        
        return ret;
    }

    public static DateTime Latest(IEnumerable<DateTime> array)
    {
        var ret = DateTime.MinValue;
        foreach (var date in array)
            if (date > ret)
                ret = date;
        return ret;
    }
    
    public static TimeSpan ParseCueTime(string timecode)
    {
        // mm:ss:ff (75 frames per second)
        var parts = timecode.Split(':');
        if (parts.Length != 3) return TimeSpan.Zero;

        int mm = int.Parse(parts[0]);
        int ss = int.Parse(parts[1]);
        int ff = int.Parse(parts[2]);

        return TimeSpan.FromSeconds(mm * 60 + ss + ff / 75.0);
    }

    public static (double start, double end) GetCueTrackTimes(TimeSpan tStart, TimeSpan? tEnd, TimeSpan tDuration)
    {
        if (tDuration.Ticks <= 0)
            return (0, 1);
        
        var start= (double) tStart.Ticks / tDuration.Ticks;
        var end = tEnd.HasValue ? (double)tEnd.Value.Ticks / tDuration.Ticks : 1;
        start = Math.Clamp(start, 0, 1);
        end   = Math.Clamp(end, start, 1);
        return (start, end);
    }
}