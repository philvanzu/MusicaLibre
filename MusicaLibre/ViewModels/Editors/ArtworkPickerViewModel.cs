using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.Views;
using System.IO;
using System.Threading;

namespace MusicaLibre.ViewModels;

public partial class ArtworkPickerViewModel:ArtworkListManagerViewModel
{
    private ArtworkPickerDialog _window;
    
    [ObservableProperty] private string? _filePath;
    [ObservableProperty] string _rootDirectory;
    
    public List<Track> _trackss;
    LibraryViewModel _library;
    private ArtworkRole _role;

    public ArtworkPickerViewModel(LibraryViewModel library, ArtworkPickerDialog window, string rootDirectory, ArtworkRole role, Artwork? selected=null)
    {
        _window = window;
        _library = library;
        _role = role;
        if(!Directory.Exists(rootDirectory)) Directory.CreateDirectory(rootDirectory);
        RootDirectory = rootDirectory;
        
        if(selected != null)
            foreach (var artwork in Artworks)
                if(artwork.Artwork == selected)
                    artwork.IsSelected = true;

        _window.Closing += (s, e) => Dispose();
    }


    partial void OnRootDirectoryChanged(string value)
    {
        Dispose();
        Artworks.Clear();

        var artworks = _library.Data.Artworks.Values
            .Where(x => PathUtils.IsDescendantPath(_rootDirectory, x.Folder.Name))
            .Select(x=> new ArtworkViewModel(this, x));
        
        Artworks.AddRange(artworks);
    }

    
    protected override void SelectedArtworkChanged(ArtworkViewModel? value)
    {
        foreach (var item in Artworks)
            if(value != item)item.IsSelected = false;

        FilePath = value?.Artwork.SourcePath;
    }

    [RelayCommand]
    async Task Browse()
    {
        var selectedPath = await DialogUtils.PickFileAsync(_window, _rootDirectory,new[]
        {
            new FilePickerFileType("Image Files") { Patterns = new[] { "*.jpg",  "*.jpeg", "*.png",  "*.bmp",  "*.gif", "*.webp" } },
            FilePickerFileTypes.All // built-in “All files (*.*)”
        } );
        if (!string.IsNullOrEmpty(selectedPath))
        {
            string? folderPath = Path.GetDirectoryName(selectedPath);
            string? ext = Path.GetExtension(selectedPath);
            if(folderPath == null) return;
            
            var folder = _library.Data.Folders.Values.FirstOrDefault(x=>x.Name.Equals(folderPath));
            var artwork = _library.Data.Artworks.Values.FirstOrDefault(x => x.SourcePath.Equals(selectedPath));
            if (artwork == null && File.Exists(selectedPath))
            {
                if (!PathUtils.IsDescendantPath(_rootDirectory, selectedPath))
                {
                    var filename = Path.GetFileName(selectedPath);
                    var importPath = Path.Combine(_library.Path, AppData.Instance.UserSettings.ImagesImportPath, filename);
                    folder = _library.Data.Folders.Values.FirstOrDefault(x=>x.Name.Equals(importPath));
                    if (folder is null)
                    {
                        folder = new Folder(importPath); 
                        await folder.DbInsertAsync(_library.Database);
                        _library.Data.Folders.Add(folder.DatabaseIndex, folder);
                    }
                    File.Move(selectedPath, importPath);
                    selectedPath = importPath;
                }
                Debug.Assert(folder is not null);
                artwork = new Artwork(_library.Database)
                {
                    SourcePath = selectedPath,
                    SourceType = ArtworkSourceType.External,
                    MimeType = PathUtils.GetMimeType(ext),
                    Folder = folder,
                };
                
                artwork.ProcessImage();
                if (artwork.Hash == null) throw new Exception("Could not process image");
                var existing = _library.Data.Artworks.Values.FirstOrDefault(x => x.Hash.Equals(artwork.Hash));
                if (existing is null)
                {
                    await artwork.DbInsertAsync(_library.Database);
                }
                else
                {
                    artwork = existing;
                }
            }
            
            Debug.Assert(artwork is not null);
            artwork.Role =  _role;
            RootDirectory = artwork.Folder?.Name;
            
            
            foreach (var vm in Artworks)
                vm.IsSelected = vm.Artwork == artwork;
        }
    }

    [RelayCommand]
    void Ok()
    {
        _window.Close(SelectedArtwork?.Artwork);
    }

    [RelayCommand]
    void Cancel()
    {
        _window.Close(null);
    }


    
    
}