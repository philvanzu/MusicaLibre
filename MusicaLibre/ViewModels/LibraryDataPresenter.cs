using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicaLibre.Models;

namespace MusicaLibre.ViewModels;


public abstract partial class LibraryDataPresenter:ViewModelBase
{
    [ObservableProperty] protected LibraryViewModel _library;
    protected List<Track>? _selectedTracks;
    public List<Track> TracksPool { get; init; }
    
    public virtual List<Track>? SelectedTracks
    {
        get => _selectedTracks;
        set => SetProperty(ref _selectedTracks, value);
    }
    [ObservableProperty] private bool _ascending;

    public LibraryDataPresenter(LibraryViewModel library, List<Track> tracksPool)
    {
        _library = library;
        TracksPool = tracksPool;
    }
    public abstract void Reverse();

    public abstract NavCapsuleViewModel? GetCapsule();
    public NavCapsuleViewModel? PreviousCapsule { get; set; }
}