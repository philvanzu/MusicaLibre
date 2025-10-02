using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.Views;

namespace MusicaLibre.ViewModels;

public partial class LibrarySettingsViewModel: ViewModelBase
{
    [ObservableProperty] string? _libraryRoot;
    [ObservableProperty] private bool _canCreateLibrary;
    public LibrarySettingsEditorDialog Window { get; set; }
    private const string defaultOrderings = @"""CustomOrderings"":[{""IsDefault"":false,""Name"":""Albums by Artists"",""Steps"":[{""Type"":0,""SortingKeys"":[{""Kind"":0,""Asc"":true,""SelectedKey"":1},{""Kind"":0,""Asc"":true,""SelectedKey"":2},{""Kind"":0,""Asc"":true,""SelectedKey"":0}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Album"",""OrderedByString"":""Ordered by Artist Name asc""}],""TracksStep"":{""Type"":12,""SortingKeys"":[{""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Kind"":1,""Asc"":true,""SelectedKey"":5}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Track"",""OrderedByString"":""Ordered by Album asc""}},{""IsDefault"":false,""Name"":""Albums by Last Modified desc"",""Steps"":[{""Type"":0,""SortingKeys"":[{""Kind"":0,""Asc"":false,""SelectedKey"":6}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Album"",""OrderedByString"":""Ordered by Date Modified desc""}],""TracksStep"":{""Type"":12,""SortingKeys"":[{""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Kind"":1,""Asc"":true,""SelectedKey"":5}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Track"",""OrderedByString"":""Ordered by Album asc""}},{""IsDefault"":false,""Name"":""Tracks by Path"",""Steps"":[{""Type"":12,""SortingKeys"":[{""Kind"":1,""Asc"":true,""SelectedKey"":4},{""Kind"":1,""Asc"":true,""SelectedKey"":3}],""TracksSortingKeys"":[{""Key"":4,""Kind"":1,""Asc"":true,""SelectedKey"":4},{""Key"":3,""Kind"":1,""Asc"":true,""SelectedKey"":3}],""GroupedByString"":""Grouped by Track"",""OrderedByString"":""Ordered by Folder asc""}],""TracksStep"":{""Type"":12,""SortingKeys"":[{""Kind"":1,""Asc"":true,""SelectedKey"":3}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Track"",""OrderedByString"":""Ordered by FilePath asc""}},{""IsDefault"":false,""Name"":""Playlists"",""Steps"":[{""Type"":11,""SortingKeys"":[{""Kind"":2,""Asc"":true,""SelectedKey"":1}],""TracksSortingKeys"":[{""Key"":25,""Kind"":1,""Asc"":true,""SelectedKey"":25}],""GroupedByString"":""Grouped by Playlist"",""OrderedByString"":""Ordered by Path asc""}],""TracksStep"":{""Type"":12,""SortingKeys"":[{""Kind"":1,""Asc"":true,""SelectedKey"":25}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Track"",""OrderedByString"":""Ordered by PlaylistPosition asc""}},{""IsDefault"":false,""Name"":""Year / Album"",""Steps"":[{""Type"":2,""SortingKeys"":[{""Kind"":3,""Asc"":true,""SelectedKey"":0}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Year"",""OrderedByString"":""Ordered by Name asc""},{""Type"":0,""SortingKeys"":[{""Kind"":0,""Asc"":true,""SelectedKey"":0}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Album"",""OrderedByString"":""Ordered by Title asc""}],""TracksStep"":{""Type"":12,""SortingKeys"":[{""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Kind"":1,""Asc"":true,""SelectedKey"":5}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Track"",""OrderedByString"":""Ordered by Album asc""}},{""IsDefault"":false,""Name"":""Genre / Album"",""Steps"":[{""Type"":4,""SortingKeys"":[{""Kind"":3,""Asc"":true,""SelectedKey"":0}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Genre"",""OrderedByString"":""Ordered by Name asc""},{""Type"":0,""SortingKeys"":[{""Kind"":0,""Asc"":true,""SelectedKey"":1},{""Kind"":0,""Asc"":true,""SelectedKey"":2},{""Kind"":0,""Asc"":true,""SelectedKey"":0}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Album"",""OrderedByString"":""Ordered by Artist Name asc""}],""TracksStep"":{""Type"":12,""SortingKeys"":[{""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Kind"":1,""Asc"":true,""SelectedKey"":5}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Track"",""OrderedByString"":""Ordered by Album asc""}},{""IsDefault"":false,""Name"":""Publisher / Album"",""Steps"":[{""Type"":5,""SortingKeys"":[{""Kind"":3,""Asc"":true,""SelectedKey"":0}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Publisher"",""OrderedByString"":""Ordered by Name asc""},{""Type"":0,""SortingKeys"":[{""Kind"":0,""Asc"":true,""SelectedKey"":2},{""Kind"":0,""Asc"":true,""SelectedKey"":0}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Album"",""OrderedByString"":""Ordered by Year asc""}],""TracksStep"":{""Type"":12,""SortingKeys"":[{""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Kind"":1,""Asc"":true,""SelectedKey"":5}],""TracksSortingKeys"":[{""Key"":1,""Kind"":1,""Asc"":true,""SelectedKey"":1},{""Key"":6,""Kind"":1,""Asc"":true,""SelectedKey"":6},{""Key"":5,""Kind"":1,""Asc"":true,""SelectedKey"":5}],""GroupedByString"":""Grouped by Track"",""OrderedByString"":""Ordered by Album asc""}}]";
    public enum LibCreationAddedDateSources {now, fromCreated, fromModified}
    public LibCreationAddedDateSources LibCreationAddedDateSource { get; set; } = LibCreationAddedDateSources.fromModified;
    [JsonIgnore] public static string[] DateSources { get; } = EnumUtils.GetDisplayNames<LibCreationAddedDateSources>();
    public string ArtistArtworkPath { get; set; } = "_Artwork/Artists";
    public string YearArtworkPath { get; set; } =  "_Artwork/Years";
    public string GenreArtworkPath { get; set; } =  "_Artwork/Genres";
    public string PublisherArtworkPath { get; set; } =   "_Artwork/Publishers";
    public string UserPlaylistsPath { get; set; } =  "_MixTapes";
    
    private List<CustomOrdering> _customOrderings = new() { CustomOrdering.Default, };
    public List<CustomOrdering> CustomOrderings
    {
        get { return _customOrderings; }
        set
        {
            SetProperty(ref _customOrderings, value);
            OnPropertyChanged(nameof(OrderingOptions));
        }
    }
    [ObservableProperty] int _selectedOrdering;
    [JsonIgnore] public ObservableCollection<string> OrderingOptions =>
        new ObservableCollection<string>(CustomOrderings.Select(x => x.Name));

    public LibrarySettingsViewModel()
    {
    }

    public void LoadDefaultOrderings()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Full resource name is usually: "<DefaultNamespace>.<Folder>.<FileName>"
            string resourceName = "MusicaLibre.Assets.default_orderings.json";

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                Console.WriteLine($"Resource '{resourceName}' not found.");
                return;
            }
            using StreamReader reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            if(json == null) throw new Exception("Settings not found");
            
            var options = new JsonSerializerOptions() {Converters = { new SortingKeyConverter() }, };
            var orderings = JsonSerializer.Deserialize<List<CustomOrdering>>(json, options);
            if(orderings == null) throw new Exception("Settings not found");
            CustomOrderings = orderings;
        }
        catch(Exception ex) {Console.WriteLine(ex);}
    }
    
    public static LibrarySettingsViewModel Load(Database db)
    {
        
        try
        {
            var sql = $"SELECT Settings FROM Info";
            var rows = db.ExecuteReader(sql);
            if(rows.Count > 1 || rows.Count <=0) throw new Exception("Corrupted info table");
            var json = Database.GetString(rows[0], "Settings");
            if(json == null) throw new Exception("Settings not found");
            
            var options = new JsonSerializerOptions()
            {
                Converters = { new SortingKeyConverter() },
            };
            var settings = JsonSerializer.Deserialize<LibrarySettingsViewModel>(json, options);
            if(settings == null) throw new Exception("Settings not found");
            return settings;
        }
        catch(Exception ex) {Console.WriteLine(ex);}


        return CreateSettings(db);
        

    }

    public static LibrarySettingsViewModel CreateSettings(Database db)
    {
        var settings = new LibrarySettingsViewModel();
        settings.LoadDefaultOrderings();
        settings.Save(db);
        return settings;
    }

    public void Save(Database db)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new SortingKeyConverter() },
            };
            string json = JsonSerializer.Serialize(this,  options);
            if (!string.IsNullOrEmpty(json))
            {
                var sql = $"UPDATE Info SET Settings = $json";
                var parameters = new Dictionary<string, object?>()
                {
                    ["$json"] = json
                };
                db.ExecuteNonQuery(sql,parameters, false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    public void RefreshOrderings()
    {
        OnPropertyChanged(nameof(CustomOrderings));
        OnPropertyChanged(nameof(OrderingOptions));
        //OnPropertyChanged(nameof(SelectedOrdering));
    }

    partial void OnLibraryRootChanged(string? value)
    {
        CanCreateLibrary = (!string.IsNullOrEmpty(LibraryRoot) && Directory.Exists(LibraryRoot));
    }

    [RelayCommand]
    async Task PickDirectory()
    {
        LibraryRoot = await DialogUtils.PickDirectoryAsync(Window);
    }

    [RelayCommand]
    void CreateLibrary()
    {
        Window.Close(LibraryRoot);
    }

    [RelayCommand]
    void Cancel()
    {
        Window.Close();
    }
    
    
}