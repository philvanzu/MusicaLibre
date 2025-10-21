using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using MusicaLibre.Models;

namespace MusicaLibre.ViewModels;

public partial class GenresEditorViewModel:ViewModelBase, IDisposable
{
    LibraryViewModel _library;
    [ObservableProperty] ObservableCollection<Genre> _genres;
    [ObservableProperty] private string _poolFilter;
    [ObservableProperty] int _selectedGenreIndex;
    [ObservableProperty] Genre? _selectedGenre;
    [ObservableProperty] ObservableCollection<Track>? _selectedTracks;
    [ObservableProperty] private string? _text;
    
    public Bitmap? Thumbnail => SelectedGenre?.Artwork?.Thumbnail;
    public Genre? _oldGenre;
    public GenresEditorViewModel(LibraryViewModel library)
    {
        _library = library;
        _genres = new(library.Data.Genres.Values);
    }

    partial void OnPoolFilterChanged(string value)
    {
        var filtered = _library.Data.Genres.Values.Where(g => g.Name.Contains(value, StringComparison.InvariantCultureIgnoreCase));
        Genres = new ObservableCollection<Genre>(filtered);
    }

    partial void OnSelectedGenreIndexChanged(int value)
    {
        if(_oldGenre != null)
            _oldGenre.Artwork?.ReleaseThumbnail(this);
        
        if (value >= 0 && value < Genres.Count)
        {
            SelectedGenre = _oldGenre =Genres[value];
            Text = SelectedGenre.Name;
            SelectedGenre.Artwork?.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Thumbnail)));
            SelectedTracks = new (_library.Data.Tracks.Values.Where(x=>x.Genres.Contains(SelectedGenre)));
        }
        else
        {
            SelectedGenre = null;
            SelectedTracks = null;
        }
    }

    public void Dispose()
    {
        SelectedGenre?.Artwork?.ReleaseThumbnail(this);
    }
    
    [RelayCommand] void PickArtwork(){}

    [RelayCommand]
    void Rename()
    {
        if (Text is null || SelectedGenre == null) return;
        
        SelectedGenre?.Artwork?.ReleaseThumbnail(this);
        _library.DeleteGenre(SelectedGenre!);

        var split = Text.Split().Select(x => x.Trim());
        if (SelectedTracks != null && SelectedTracks.Count > 0)
        {
            Genre? genre = null;
            foreach (var name in split)
            {
                var g = _library.Data.Genres.Values
                    .FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (g is null )
                    g = _library.CreateGenre(name, SelectedTracks.ToList());
                else
                    _library.AddGenreToTracks(g, SelectedTracks.ToList());
                if(genre == null) genre = g;
            }
            _genres.Clear();
            _genres.AddRange(_library.Data.Genres.Values);
            if (genre != null)
                SelectedGenreIndex = Genres.IndexOf(genre);
        }
         
    }

    [RelayCommand]
    void Delete()
    {
        SelectedGenre?.Artwork?.ReleaseThumbnail(this);
        _library.DeleteGenre(SelectedGenre!);
        SelectedGenreIndex = -1;
    }

    [RelayCommand]
    void Add()
    {
        if (!string.IsNullOrWhiteSpace(Text))
        {
            var genre = _library.CreateGenre(Text);
            _genres.Clear();
            _genres.AddRange(_library.Data.Genres.Values);
            if (genre != null)
                SelectedGenreIndex = Genres.IndexOf(genre);
        }
    }


    [RelayCommand]
    void ArrowUp()
    {
        var index = SelectedGenreIndex - 1;
        if (index >=Genres.Count || index < 0) index = Genres.Count-1;
        SelectedGenreIndex = index;
    }
    [RelayCommand]
    void ArrowDown()
    {
        var index = SelectedGenreIndex + 1;
        if (index >=Genres.Count || index < 0) index = 0;
        SelectedGenreIndex = index;
    }
}