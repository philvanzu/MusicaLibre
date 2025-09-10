using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public partial class AlbumsEditorViewModel:ViewModelBase, IDisposable
{
    [ObservableProperty] LibraryViewModel _library;
    [ObservableProperty] private ObservableCollection<Album> _albums;
    [ObservableProperty] private ObservableCollection<DiscViewModel>? _selectedDiscs;
    [ObservableProperty] private Album? _selectedAlbum;
    [ObservableProperty] private int _selectedIndex;
    [ObservableProperty] private string? _title;
    [ObservableProperty] private string? _artist;
    [ObservableProperty] private string? _folder;
    [ObservableProperty] private string? _year;
    [ObservableProperty] private string? _added;
    [ObservableProperty] private string? _modified;
    [ObservableProperty] private string? _played;
    [ObservableProperty] private string? _created;
    [ObservableProperty] private ObservableCollection<Track>? _selectedTracks;
    public Bitmap? Thumbnail=>SelectedAlbum?.Cover?.Thumbnail;

    public AlbumsEditorViewModel(TagsEditorViewModel tagsEditor, List<Album> albums)
    {
        _albums = new ObservableCollection<Album>(albums);
        Library = tagsEditor.Library;
        SelectedIndex = 0;
        SelectedAlbum = albums[0];
    }

    partial void OnSelectedIndexChanged(int value)
    {
        if(value >=0 && value < Albums.Count)
            SelectedAlbum = Albums[value];
        else SelectedAlbum = null;
    }

    Artwork? _oldArtwork;
    partial void OnSelectedAlbumChanged(Album? value)
    {
        _oldArtwork?.ReleaseThumbnail(this);
        if(SelectedDiscs != null)
        {
            foreach (var discvm in SelectedDiscs)
                discvm.Dispose();
            SelectedDiscs = null;
        }
        if (value?.Cover != null)
        {
            value.Cover.RequestThumbnail(this,()=>OnPropertyChanged(nameof(Thumbnail)));
            _oldArtwork = value.Cover;
        }
        
        Title = value?.Title ;
        Artist = value?.AlbumArtist.Name;
        Year = value?.Year?.Name;
        Folder = value?.Folder?.Name;
        Added =  TimeUtils.FormatDateTime(value?.Added);
        Modified = TimeUtils.FormatDateTime(value?.Modified);
        Created = TimeUtils.FormatDateTime(value?.Created);
        Played = TimeUtils.FormatDateTime(value?.LastPlayed);
        if (value != null)
        {
            SelectedTracks = new(Library.Tracks.Values.Where(x => x.Album == SelectedAlbum));
            SelectedDiscs = new (Library.Discs.Values.Where(x=> x.Album == value)
                .Select(x=>new DiscViewModel(x)));
        }
    }

    public void Dispose()
    {
        _oldArtwork?.ReleaseThumbnail(this);
        if(SelectedDiscs != null)
        {
            foreach (var discvm in SelectedDiscs)
                discvm.Dispose();
            SelectedDiscs = null;
        }
    }


    [RelayCommand]
    async Task UpdatePressed()
    {
        if (SelectedAlbum == null) return;
        
        if(!string.IsNullOrEmpty(Title)) SelectedAlbum.Title = Title;
        if (!string.IsNullOrEmpty(Artist))
        {
            var artist = Library.Artists.Values
                .FirstOrDefault(x=>x.Name!.Equals(Artist, StringComparison.OrdinalIgnoreCase));
            if (artist is null)
            {
                artist = new Artist(Artist);
                await artist.DbInsertAsync(Library.Database);
            }
            SelectedAlbum.AlbumArtist = artist;
        }

        if (uint.TryParse(Year, out var yearNumber))
        {
            var year = Library.Years.Values
                .FirstOrDefault(x => x.Name!.Equals(Year, StringComparison.OrdinalIgnoreCase));
            if (year is null)
            {
                year = new Year(yearNumber);
                await year.DbInsertAsync(Library.Database);
            }
            SelectedAlbum.Year = year;
            if (SelectedTracks != null)
            {
                foreach (var track in SelectedTracks)
                {
                    track.Year = year;
                    _ = track.DbUpdateAsync(Library.Database);
                }
            }
        }

        SelectedAlbum.Added = TimeUtils.FromDateTimeString(Added);
        SelectedAlbum.Modified = TimeUtils.FromDateTimeString(Modified);
        SelectedAlbum.Created = TimeUtils.FromDateTimeString(Created);
        SelectedAlbum.LastPlayed = TimeUtils.FromDateTimeString(Played);

        _ = SelectedAlbum.DbUpdateAsync(Library.Database);
    }

}