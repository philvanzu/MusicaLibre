using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using MusicaLibre.Models;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Services;
using System.IO;

public class AppData: IDisposable
{
    private static readonly string LinuxFilePath =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local/share/Musicalibre/");
    
    static readonly AppData _instance = AppData.Load(); 
    public static AppData Instance => _instance; // Called On Framework init completed

    [JsonIgnore] public List<string> Libraries { get; set; } = new();
    public AppState AppState { get; set; } = new();
    public UserSettings UserSettings { get; set; } = new();
    public List<ExternalDevice> ExternalDevices { get; set; } = new();
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
            ExternalDevicesManager.Start();
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
                        foreach(var dev in data.ExternalDevices)
                            dev.Initialize();
                        
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

    // Called On MainWindow Closed
    public void Dispose() 
    {
        foreach(var dev in ExternalDevices)
            dev.Dispose();
        ExternalDevicesManager.Stop();
    }
}

public class AppState
{
    public string CurrentLibrary { get; set; }=string.Empty;
    public PixelPoint WindowPosition { get; set; } = new PixelPoint(100, 100);
    public double WindowWidth { get; set; } = 800;
    public double WindowHeight { get; set; } = 600;
    public WindowState WindowState { get; set; } = WindowState.Normal;
    public double Volume { get; set; } = 1;
    public List<long> NowPlayingTrackIds { get; set; } = new();
    public long? NowPlayingTrackId { get; set; }
}

public class UserSettings
{
    
    public string ImagesImportPath { get; set; } = "/ImportedImages";
    public bool FilterOutEmptyPlaylists { get; set; } = true;
    
}



