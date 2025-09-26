using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.Views;
using TagLib;

namespace MusicaLibre.ViewModels;

public partial class TrackArtworkManagerViewModel:ArtworkListManagerViewModel
{
    [ObservableProperty] TrackViewModel? _selectedTrack;
    LibraryViewModel _library;
    private TagsEditorDialog _window;
    public TrackArtworkManagerViewModel(LibraryViewModel library, TagsEditorDialog window)
    {
        _library = library;
        _window = window;
    }
    
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
        var dbCount = value.Model.Artworks.Count;
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
            var existing = track.Artworks.FirstOrDefault(x=>x.Hash == artwork.Hash);
            if(existing != null) artwork = existing;
            var vm = new ArtworkViewModel(this, artwork);
            Artworks.Add(vm);
        }
    }

    protected override void SelectedArtworkChanged(ArtworkViewModel? value)
    {
        
    }

    [RelayCommand]
    async Task Remove()
    {
        if (SelectedArtwork == null || SelectedTrack == null) return;
        Artworks.Remove(SelectedArtwork);
        var art = SelectedArtwork.Artwork;
        var index = art.EmbedIdx;
        using var file = TagLib.File.Create(art.SourcePath);
        var pictures = file.Tag.Pictures.ToList() ?? new List<IPicture>();
        if (index.HasValue && index >= 0 && index < pictures?.Count)
        {
            pictures.RemoveAt(index.Value);
            file.Tag.Pictures = pictures.ToArray();
            file.Save();
        }
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
        using var file = TagLib.File.Create(SelectedTrack.Model.FilePath);

        var pictures = file.Tag.Pictures?.ToList() ?? new List<IPicture>();
        var newPic = new Picture(selectedPath)
        {
            Type = PictureType.FrontCover,          // cover art
            Description = "Cover",
            MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg
        };

        pictures.Add(newPic);
        file.Tag.Pictures = pictures.ToArray();
        file.Save();
    }
}