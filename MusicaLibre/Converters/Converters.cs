
using Avalonia.Data.Converters;
using System;
namespace MusicaLibre.Converters;
public static class Converters
{
    public static readonly FuncValueConverter<bool, string> AscendingArrowConverter =
        new FuncValueConverter<bool, string>(asc => asc ? "▲" : "▼");

}
