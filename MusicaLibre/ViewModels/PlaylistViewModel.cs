using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;

namespace MusicaLibre.ViewModels;

public partial class PlaylistViewModel:ViewModelBase, IVirtualizableItem
{
    [ObservableProperty] PlaylistsListViewModel _presenter;
    public List<Track> Tracks=>Model.Tracks.OrderBy(x => x.position).Select(x=>x.track).ToList();
    public string Count => $"{Tracks.Count} tracks"; 
    public Playlist Model { get; init; }
    public string FileName => Model.FileName??"";
    public string FilePath => Model.FilePath??"";
    public Bitmap? Thumbnail => Artwork?.Thumbnail;
    [ObservableProperty]Artwork? _artwork;

    public Artwork? FindArtwork()
    {
        return Presenter.Library.Artworks.Values.Where(x => x.Folder == Model.Folder).FirstOrDefault();
    }
    public int RandomIndex { get; set; }
    public bool IsSelected { get; set; }
    public bool IsFirst => Presenter.GetItemIndex(this) == 0; 
    public bool IsPrepared { get; set; }

    public PlaylistViewModel(PlaylistsListViewModel presenter, Playlist playlist)
    {
        Model = playlist;
        Presenter = presenter;
        Artwork = FindArtwork();
    }
    public void OnPrepared()
    {
        IsPrepared = true;
        if(Artwork != null)
            Artwork.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Thumbnail)));
    }

    public void OnCleared()
    {
        if(Artwork != null)
            Artwork.ReleaseThumbnail(this);
    }

    [RelayCommand]
    void OpenInExplorer()
    {
        try
        {
            var folderPath = System.IO.Path.GetDirectoryName(FilePath);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", string.Format("/select,\"{0}\"", FilePath));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string? desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")
                                  ?? Environment.GetEnvironmentVariable("DESKTOP_SESSION")
                                  ?? string.Empty;
                desktop = desktop.ToLowerInvariant();
                
                //GNOME (Nautilus)
                if (desktop.Contains("gnome") || desktop.Contains("unity") || desktop.Contains("cinnamon"))
                    Process.Start("nautilus", $"--select \"{FilePath}\"");
                //Dolphin (KDE)
                else if (desktop.Contains("kde"))
                    Process.Start("dolphin", $"--select \"{FilePath}\"");
                // Try with xdg-open (common across most Linux desktop environments)
                else if(Directory.Exists(folderPath))
                    Process.Start(new ProcessStartInfo("xdg-open", $"\"{folderPath}\"") { UseShellExecute = true });    
            }
            else
            {
                throw new PlatformNotSupportedException("Only Windows and Linux are supported.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to open path: {ex.Message}");
        }
    }
    [RelayCommand]void Edit(){}

    [RelayCommand]void DoubleTapped()=>Presenter.Library.ChangeOrderingStep(Presenter);

    [RelayCommand] void Play()=>Presenter.Library.NowPlayingList.Replace(Tracks);
    [RelayCommand] void InsertNext()=>Presenter.Library.NowPlayingList.Insert(Tracks);
    [RelayCommand] void Append()=>Presenter.Library.NowPlayingList.Append(Tracks);
}