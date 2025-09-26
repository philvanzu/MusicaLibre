using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicaLibre.Models;

namespace MusicaLibre.ViewModels;

public partial class ArtistsEditorViewModel: ViewModelBase, IDisposable
{
    LibraryViewModel _library;
    [ObservableProperty] ObservableCollection<Artist> _artists;
    [ObservableProperty] int _selectedArtistIndex;
    [ObservableProperty] Artist? _selectedArtist;
    [ObservableProperty] ObservableCollection<Track>? _selectedArtistTracks;
    [ObservableProperty] private string? _text;
    public void Dispose()
    {
        
    }
}