using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using MusicaLibre.Models;

namespace MusicaLibre.Services;
using System.IO;

public class AppData
{
    private static readonly string LinuxFilePath =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local/share/Musicalibre/");
    
    static readonly AppData _instance = AppData.Load();
    public static AppData Instance => _instance;

    [JsonIgnore] public List<string> Libraries { get; set; } = new();
    public AppState AppState { get; set; } = new();
    public UserSettings UserSettings { get; set; } = new();
    
    public static string Path
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if(!Directory.Exists(LinuxFilePath))
                    Directory.CreateDirectory(LinuxFilePath);
                return LinuxFilePath;
            }
            return string.Empty;
        }
    }
    
    public AppData() {}

    private static AppData Load()
    {
        try
        {
            DirectoryInfo appdataDir = new DirectoryInfo(Path);
            if (!appdataDir.Exists) throw new DirectoryNotFoundException(Path);
            
            List<string> libraries = new();
            foreach (var file in appdataDir.GetFiles())
            {
                if(file.Extension == ".db")
                    libraries.Add(file.FullName);
                else if (file.Name.Equals("appdata.json"))
                {
                    var json = File.ReadAllText(file.FullName);
                    var data = JsonSerializer.Deserialize<AppData>(json);
                    if (data != null)
                    {
                        data.Libraries = libraries;
                        return data;
                    }
                }
            }    
        }
        catch(Exception ex) {Console.WriteLine(ex);}
        return new AppData();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this);
            var appdataPath = System.IO.Path.Combine(Path, "appdata.json");
            File.WriteAllText(appdataPath, json);    
        }
        catch(Exception ex) {Console.WriteLine(ex);}
        
    }
    
}

public class AppState
{
    public string CurrentLibrary { get; set; }=string.Empty;
    public PixelPoint WindowPosition { get; set; } = new PixelPoint(100, 100);
    public double WindowWidth { get; set; } = 800;
    public double WindowHeight { get; set; } = 600;
    public WindowState WindowState { get; set; } = WindowState.Normal;
}

public class UserSettings
{
    
}

