using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.Views;
using TagLib;
using File = System.IO.File;

namespace MusicaLibre.ViewModels;

public partial class TrackArtworkManagerViewModel:ViewModelBase, IDisposable, ISelectVirtualizableItems
{
    [ObservableProperty] private string? _selectionStr;
    [ObservableProperty] private ObservableCollection<TrackArtworkViewModel> _artworks = new();
    [ObservableProperty] private TrackArtworkViewModel? _selectedArtwork;
    [ObservableProperty] private ProgressViewModel _progress = new ProgressViewModel();
    

    [ObservableProperty] TrackViewModel? _selectedTrack;
    LibraryViewModel _library;
    private TagsEditorDialog _window;
    private TagsEditorViewModel _tagsEditor;
    
    public event EventHandler<SelectedItemChangedEventArgs>? SelectionChanged;
    public event EventHandler? SortOrderChanged;
    public event EventHandler<int>? ScrollToIndexRequested;
    
    public TrackArtworkManagerViewModel(TagsEditorViewModel tagsEditor, TagsEditorDialog window)
    {
        _tagsEditor = tagsEditor;
        _library = tagsEditor.Library;
        _window = window;
    }
    

    partial void OnSelectedArtworkChanged(TrackArtworkViewModel? value)
    {
        
    }

    public void Dispose()
    {
        foreach (var item in _artworks)
            item.Dispose();
    }

    public int GetItemIndex(TrackArtworkViewModel trackArtwork)=>Artworks.IndexOf(trackArtwork);
    public int GetSelectedIndex() => SelectedArtwork != null ? Artworks.IndexOf(SelectedArtwork) : -1;
    partial void OnSelectedTrackChanged(TrackViewModel? value)
    {
        SelectionStr = value?.Model.FilePath;
        foreach (var vm in  Artworks)
            vm.Dispose();
        
        Artworks.Clear();
        
        var track = value?.Model;
        if(value == null || track is null) return;
        
        using var file = TagLib.File.Create(value?.Model.FilePath);
        var embeds = file.Tag.Pictures;
        
        for (int i = 0; i < embeds.Length; i++)
        {
            var artwork = new Artwork(_library.Database)
            {
                SourcePath = track.FilePath,
                FolderPathstr = track.Folder.Name,
                Folder = track.Folder,
                SourceType = ArtworkSourceType.Embedded,
                MimeType = embeds[i].MimeType,
                Role = ArtworkRole.CoverFront,
                EmbedIdx = i,
            };
            artwork.ProcessImage(new MemoryStream(embeds[i].Data.Data));
            Bitmap? thumbnail = null;
            if (!string.IsNullOrEmpty(artwork.Hash))
            {
                using var stream = new MemoryStream(artwork.ThumbnailData);
                thumbnail = new Bitmap(stream);
                
            }
            var vm = new TrackArtworkViewModel(this, thumbnail, artwork);
            Artworks.Add(vm);
            
            
        }
    }



    [RelayCommand]
    async Task Remove()
    {
        if (SelectedArtwork == null || SelectedTrack == null) return;
        
        var index = SelectedArtwork.Index;
        using var file = TagLib.File.Create(SelectedTrack.Model.FilePath);
        var pictures = file.Tag.Pictures.ToList() ?? new List<IPicture>();
        if (index >= 0 && index < pictures.Count)
        {
            pictures.RemoveAt(index);
            file.Tag.Pictures = pictures.ToArray();
            file.Save();
        }
        
        Artworks.Remove(SelectedArtwork);
        await Artwork.RemoveUninstanciatedEmbeddedArtworks(_library);
    }

    [RelayCommand]
    async Task RemoveAllEmbeddedPictures()
    {
        await Task.Run(async() =>
        {
            try
            {
                foreach (var track in _tagsEditor.SelectedItems)
                {
                    using var file = TagLib.File.Create(track.Model.FilePath);
                    file.Tag.Pictures = Array.Empty<IPicture>();
                    file.Save();
                    track.Model.Artworks.Clear();
                    await track.Model.UpdateArtworksAsync(_library);
                }                
            }
            catch (Exception ex){Console.WriteLine(ex);}
        });
        await Artwork.RemoveUninstanciatedEmbeddedArtworks(_library);
        
        foreach (var artwork in Artworks)
            artwork.Dispose();
        Artworks.Clear();
            
    }

    [RelayCommand]
    async Task Add()
    {
        if(SelectedTrack is null) return;
        
        var selectedPath = await DialogUtils.PickFileAsync(_window, SelectedTrack.Model.Folder.Name,new[]
        {
            new FilePickerFileType("Image Files") { Patterns = new[] { "*.jpg",  "*.jpeg", "*.png",  "*.bmp",  "*.gif", "*.webp" } },
            FilePickerFileTypes.All // built-in “All files (*.*)”
        } );
        if(string.IsNullOrWhiteSpace(selectedPath)) return;

        var newPic = new Picture(selectedPath)
        {
            Type = PictureType.FrontCover,          // cover art
            Description = "Cover",
            MimeType = PathUtils.GetMimeType(Path.GetExtension(selectedPath))
        };
        
        var artwork = await Artwork.InsertIfNotExist(_library, selectedPath, ArtworkRole.CoverFront, 
            ArtworkSourceType.Embedded,SelectedTrack.Model.Folder);

        foreach (var item in _tagsEditor.SelectedItems)
        {
            using var file = TagLib.File.Create(item.Model.FilePath);
            var pictures = file.Tag.Pictures?.ToList() ?? new List<IPicture>();
            pictures.Add(newPic);
            file.Tag.Pictures = pictures.ToArray();
            file.Save();

            if (artwork != null)
            {
                item.Model.Artworks.Add(artwork);
                await item.Model.UpdateArtworksAsync(_library);    
            }
        }
        
    }

    [RelayCommand]
    async Task SetSelectedAlbumCover()
    {
        if(SelectedTrack is null || SelectedArtwork is null) return;
        var existing = _library.Data.Artworks.Values.FirstOrDefault(x => x.Hash.Equals(SelectedArtwork.Artwork.Hash));
        if (existing == null)
        {
            var track = SelectedTrack.Model;
            existing = SelectedArtwork.Artwork;
            await existing.DbInsertAsync(_library.Database);
            _library.Data.Artworks.Add(existing.DatabaseIndex, existing);
        }

        SelectedTrack.Model.Album.Cover = existing;
        await SelectedTrack.Model.Album.DbUpdateAsync(_library.Database);
        await DialogUtils.MessageBox(_window, "Success!", "Album Cover updated");
    }
}