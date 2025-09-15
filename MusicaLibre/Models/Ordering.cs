using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Models;

public enum OrderGroupingType
{
    Album, Disc, Year, Artist, Genre, Publisher, Remixer, Composer, Conductor, Folder,
    [Display(Name = "Bitrate / Format")] Bitrate_Format, Playlist, Track,
}

public enum TrackSortKeys
{
    Title, Album, Artists, FilePath, Folder,
    [Display(Name = "Track Number")] TrackNumber,
    [Display(Name = "Disc Number")] DiscNumber,
    Year, Genre, Publisher, Remixer, Composer, Conductor,
    [Display(Name = "Date Added")]Added,
    [Display(Name = "Date Created")] Created,
    [Display(Name = "Date Modified")] Modified,
    [Display(Name = "Date Last Played")] Played,
    Comment, Random, Duration, Bitrate, MimeType, Codec, SampleRate, Channels, PlaylistPosition,
}

public enum AlbumSortKeys { Title, 
    [Display(Name = "Artist Name")]ArtistName, 
    Year,  RootFolder, 
    [Display(Name = "Date Added")]Added,
    [Display(Name = "Date Created")] Created,
    [Display(Name = "Date Modified")] Modified,
    [Display(Name = "Date Last Played")] LastPlayed,
    Random}
public enum NameSortKeys { Name, Random}

public enum PlaylistSortKeys
{
    Name, Path,  Random,
    [Display(Name = "Date Added")]Added,
    [Display(Name = "Date Created")] Created,
    [Display(Name = "Date Modified")] Modified,
    [Display(Name = "Date Last Played")] LastPlayed,
}

public enum SortKeyKind {Album, Track, Playlist, Name}
public abstract class SortingKey
{
    public static string[] AscOptions  => new []{"desc", "asc"};
    public SortKeyKind Kind { get; set; }
    public bool Asc { get; private set; }
    public int SelectedKey { get; set; }

    [JsonIgnore]public string AscString => Asc ? "asc" : "desc";
    [JsonIgnore]public int SelectedAsc
    {
        get => Asc? 1 : 0;
        set => Asc= (value == 1) ? true : false;
    }
    [JsonIgnore]public string[] SortingOptions => GetSortingOptions();
    
    protected SortingKey(SortKeyKind kind, bool asc = true)
    {
        Kind = kind;
        Asc = asc;
    }
    
    protected abstract string[] GetSortingOptions();
    public abstract override string ToString();
}
public class SortingKey<TEnum> :SortingKey where TEnum : Enum
{
    public TEnum Key
    {
        get => (TEnum)Enum.ToObject(typeof(TEnum), SelectedKey); // base field
        set => SelectedKey = Convert.ToInt32(value);              // updates base._selectedKey
    }
    public SortingKey(TEnum key, bool asc = true): base(GetKindFromType(typeof(TEnum)),asc) => Key = key;
    protected override string[] GetSortingOptions() => EnumUtils.GetDisplayNames<TEnum>();
    private static SortKeyKind GetKindFromType(Type type) =>
        type == typeof(AlbumSortKeys)  ? SortKeyKind.Album :
        type == typeof(TrackSortKeys)  ? SortKeyKind.Track :
        type == typeof(PlaylistSortKeys)   ? SortKeyKind.Playlist :
        type == typeof(NameSortKeys)   ? SortKeyKind.Name :
        throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported enum type {type}");
    public override string ToString()
    {
        return $"{EnumUtils.GetDisplayName(Key)} {AscString}";
    }
}

public class SortingKeyConverter : JsonConverter<SortingKey>
{
    public override SortingKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            var root = doc.RootElement;
            var kind = root.GetProperty("Kind").Deserialize<SortKeyKind>(options);
            var keyval = root.GetProperty("SelectedKey").Deserialize<int>(options);
            var asc = root.GetProperty("Asc").Deserialize<bool>(options);
            
            return kind switch
            {
                SortKeyKind.Album => new SortingKey<AlbumSortKeys>( (AlbumSortKeys)Enum.ToObject(typeof(AlbumSortKeys), keyval), asc),
                SortKeyKind.Track => new SortingKey<TrackSortKeys>( (TrackSortKeys)Enum.ToObject(typeof(TrackSortKeys), keyval), asc),
                SortKeyKind.Playlist => new SortingKey<PlaylistSortKeys>( (PlaylistSortKeys)Enum.ToObject(typeof(PlaylistSortKeys), keyval), asc),
                SortKeyKind.Name => new SortingKey<NameSortKeys>( (NameSortKeys)Enum.ToObject(typeof(NameSortKeys), keyval), asc),
                _ => throw new JsonException($"Unknown SortingKey kind '{kind}'")
            };
        }
    }

    public override void Write(Utf8JsonWriter writer, SortingKey value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Kind discriminator
        writer.WritePropertyName("Kind");
        JsonSerializer.Serialize(writer, value.Kind, options);
        // Asc flag
        writer.WriteBoolean("Asc", value.Asc);
        // SelectedKey index
        writer.WriteNumber("SelectedKey", value.SelectedKey);

        writer.WriteEndObject();
    }
}

public class OrderingStep
{
    public OrderGroupingType Type {get;set;}
    public List<SortingKey> SortingKeys {get;set;} = new();
    public List<SortingKey<TrackSortKeys>> TracksSortingKeys { get; set; }

    public string GroupedByString =>$"Grouped by {EnumUtils.GetDisplayName(Type)}";

    public string OrderedByString=>$"Ordered by {SortingKeys.FirstOrDefault()}";
    

    public OrderingStep()
    {
        TracksSortingKeys = CustomOrdering.GetDefaultTrackSortingKeys();
    }
    public override string ToString()
    {
        return $"{GroupedByString} / {OrderedByString}";
    }
    [JsonIgnore]public SortingKey DefaultKey=>Type switch
        {
            OrderGroupingType.Album => new SortingKey<AlbumSortKeys>(AlbumSortKeys.Added, true),
            OrderGroupingType.Disc => new SortingKey<AlbumSortKeys>(AlbumSortKeys.Added, true),
            OrderGroupingType.Artist => new SortingKey<NameSortKeys>(NameSortKeys.Name,  true),
            OrderGroupingType.Bitrate_Format => new SortingKey<NameSortKeys>(NameSortKeys.Name,  true),
            OrderGroupingType.Composer => new SortingKey<NameSortKeys>(NameSortKeys.Name,  true),
            OrderGroupingType.Conductor => new SortingKey<NameSortKeys>(NameSortKeys.Name,  true),
            OrderGroupingType.Folder => new SortingKey<NameSortKeys>(NameSortKeys.Name,  true),
            OrderGroupingType.Genre => new SortingKey<NameSortKeys>(NameSortKeys.Name,  true),
            OrderGroupingType.Playlist => new SortingKey<PlaylistSortKeys>(PlaylistSortKeys.Path,  true),
            OrderGroupingType.Publisher => new SortingKey<NameSortKeys>(NameSortKeys.Name,  true),
            OrderGroupingType.Remixer => new SortingKey<NameSortKeys>(NameSortKeys.Name,  true),
            OrderGroupingType.Track => new SortingKey<TrackSortKeys>(TrackSortKeys.Title,  true),
            OrderGroupingType.Year => new SortingKey<NameSortKeys>(NameSortKeys.Name,  true),
            _ => throw new NotImplementedException()
        };


}

public class CustomOrdering
{
    public bool IsDefault {get; private set;}
    public string Name {get;set;}
    public List<OrderingStep> Steps { get; set; }

    private OrderingStep _tracksStep = GetDefaultTrackStep();
    public OrderingStep TracksStep
    {
        get => _tracksStep;
        set => _tracksStep = value;
    }

    public static CustomOrdering Default = new()
    {
        Name = "Albums by Artists",
        IsDefault = true,
        Steps = new List<OrderingStep>()
        {
            new OrderingStep()
            {
                Type = OrderGroupingType.Album,
                SortingKeys = new List<SortingKey>()
                {
                    new SortingKey<AlbumSortKeys>(AlbumSortKeys.ArtistName),
                    new SortingKey<AlbumSortKeys>(AlbumSortKeys.Year),
                    new SortingKey<AlbumSortKeys>(AlbumSortKeys.Title),
                },
            },
        },
        TracksStep = GetDefaultTrackStep(),
    };

    public static OrderingStep GetDefaultTrackStep()
    {
        return new OrderingStep()
        {
            Type = OrderGroupingType.Track,
            SortingKeys =GetDefaultTrackSortingKeys().Cast<SortingKey>().ToList(),
        };
    }

    public static List<SortingKey<TrackSortKeys>> GetDefaultTrackSortingKeys() => new ()
        {
            new SortingKey<TrackSortKeys>(TrackSortKeys.Album),
            new SortingKey<TrackSortKeys>(TrackSortKeys.DiscNumber),
            new SortingKey<TrackSortKeys>(TrackSortKeys.TrackNumber),
        };

}

