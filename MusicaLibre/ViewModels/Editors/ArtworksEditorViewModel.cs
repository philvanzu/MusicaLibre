using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.Views;
using System.IO;

namespace MusicaLibre.ViewModels;

public partial class ArtworksEditorViewModel: ViewModelBase, IDisposable
{
    private LibraryViewModel _library;
    private TagsEditorDialog _window;
    private TagsEditorViewModel _tagsEditor;
    [ObservableProperty] ObservableCollection<Artwork> _artworks;
    [ObservableProperty] string _poolFilter;
    [ObservableProperty] int _selectedArtworkIndex;
    [ObservableProperty] Artwork? _selectedArtwork;
    
    public Bitmap? Thumbnail => SelectedArtwork?.Thumbnail;
    public Artwork? _oldArtwork;

    public ArtworksEditorViewModel(TagsEditorViewModel dialogvm)
    {
        _library = dialogvm.Library;
        _window = dialogvm.Window;
        _tagsEditor = dialogvm;
        Artworks = new ObservableCollection<Artwork>(_library.Data.Artworks.Values.OrderBy(x => x.SourcePath));
    }
    
    partial void OnPoolFilterChanged(string value)
    {
        _artworks.Clear();
        _artworks.AddRange(_library.Data.Artworks.Values.Where(x => x.SourcePath.Contains(value, StringComparison.OrdinalIgnoreCase)));
        SelectedArtworkIndex = -1;
    }
    partial void OnSelectedArtworkIndexChanged(int value)
    {
        if(_oldArtwork != null)
            _oldArtwork.ReleaseThumbnail(this);
        
        if (value >= 0 && value < Artworks.Count)
        {
            SelectedArtwork = _oldArtwork = Artworks[value];
            SelectedArtwork.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Thumbnail)));
        }
        else
        {
            SelectedArtwork = null;
        }
        OnPropertyChanged(nameof(Thumbnail));
    }
    
    public void Dispose()
    {
        SelectedArtwork?.ReleaseThumbnail(this);
    }

    [RelayCommand]
    async Task PickArtwork()
    {
        if (SelectedArtwork == null) return;
        
        var selectedPath = await DialogUtils.PickFileAsync(_window, SelectedArtwork.Folder.Name,new[]
        {
            new FilePickerFileType("Image Files") { Patterns = new[] { "*.jpg",  "*.jpeg", "*.png",  "*.bmp",  "*.gif", "*.webp" } },
            new FilePickerFileType("Embedded Audio File Images") { Patterns = new[]  { "*.mp3", "*.flac", "*.ape", "*.m4a", "*.ogg", "*.opus", "*.wma" } },
            FilePickerFileTypes.All // built-in “All files (*.*)”
        } );
        if (!string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath))
        {
            SelectedArtwork.ReleaseThumbnail(this);
            var success = await SelectedArtwork.ReplaceImage(selectedPath, _library, _window);
            if (success)
            {
                await DialogUtils.MessageBox(_window, "Success", "Artwork replaced successfully");
                SelectedArtwork.RequestThumbnail(this, () => OnPropertyChanged(nameof(Thumbnail)));    
            }
            else await DialogUtils.MessageBox(_window, "Error", "Artwork was not replaced successfully");
        }
    }

    [RelayCommand]
    async Task Reload()
    {
        if(SelectedArtwork is null) return;
        SelectedArtwork.ReleaseThumbnail(this);
        var success = await SelectedArtwork.ReplaceImage(SelectedArtwork.SourcePath, _library, _window);
        if (success)
        {
            await DialogUtils.MessageBox(_window, "Success", "Artwork replaced successfully");
            SelectedArtwork.RequestThumbnail(this, () => OnPropertyChanged(nameof(Thumbnail)));    
        }
        else await DialogUtils.MessageBox(_window, "Error", "Artwork was not replaced successfully");
    }

    [RelayCommand]
    async Task Delete()
    {
        if(SelectedArtwork is null) return;
        var path = SelectedArtwork.SourcePath;
        
        if (await DialogUtils.YesNoDialog(_window, "Delete?", $"Delete {path}?"))
        {
            if (SelectedArtwork.SourceType != ArtworkSourceType.Embedded && File.Exists(path))
                File.Delete(path);

            await SelectedArtwork.DbDeleteAsync(_library.Database);
            _library.Data.Artworks.Remove(SelectedArtwork.DatabaseIndex);
            Artworks.Remove(SelectedArtwork);
            SelectedArtwork = null;
        }
    }
    [RelayCommand]
    void ArrowUp()
    {
        var index = SelectedArtworkIndex - 1;
        if (index >=Artworks.Count || index < 0) index = Artworks.Count-1;
        SelectedArtworkIndex = index;
    }

    [RelayCommand]
    void ArrowDown()
    {
        var index = SelectedArtworkIndex + 1;
        if (index >=Artworks.Count || index < 0) index = 0;
        SelectedArtworkIndex = index;
    }
}