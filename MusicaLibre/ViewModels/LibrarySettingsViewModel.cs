using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicaLibre.Models;
using MusicaLibre.Services;
namespace MusicaLibre.ViewModels;

public partial class LibrarySettingsViewModel: ViewModelBase
{
    
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
}