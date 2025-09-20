using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicaLibre.Models;

namespace MusicaLibre.ViewModels;


public abstract partial class LibraryDataPresenter:ViewModelBase
{
    [ObservableProperty] protected LibraryViewModel _library;
    protected List<Track>? _selectedTracks;

    protected List<Track> _tracksPool;

    protected bool _initialized;
    public List<Track> TracksPool
    {
        get => _tracksPool;
        set
        {
            _tracksPool = value;
            if(_initialized)
                OnTracksPoolChanged();
        }
    }
    
    protected abstract void OnTracksPoolChanged();
    
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
    public abstract void Filter(string searchString);
    public NavCapsuleViewModel? PreviousCapsule { get; set; }
}