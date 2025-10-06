using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace MusicaLibre.Services;

public static class EnumUtils
{
    public static string[] GetDisplayNames<TEnum>() where TEnum : Enum
    {
        return typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f =>
            {
                var attr = f.GetCustomAttribute<DisplayAttribute>();
                return attr?.Name ?? f.Name;
            })
            .ToArray();
    }
    public static string GetDisplayName(Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        if (field is null)
            return value.ToString();

        var attr = field.GetCustomAttribute<DisplayAttribute>();
        return attr?.Name ?? value.ToString();
    }
}