using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using TagLib;
using DynamicData;
using MusicaLibre.Services;
using MusicaLibre.Views;

namespace MusicaLibre.ViewModels;

public partial class ArtistsEditorViewModel: ViewModelBase, IDisposable
{
    LibraryViewModel _library;
    [ObservableProperty] ObservableCollection<Artist> _artists;
    [ObservableProperty] string _poolFilter;
    [ObservableProperty] int _selectedArtistIndex;
    [ObservableProperty] Artist? _selectedArtist;
    [ObservableProperty] private ObservableCollection<string?> _selectedList;
    [ObservableProperty] private int _selectedRadioIdx;
    List<Track>? _selectedArtistTracks;
    [ObservableProperty] private string _tracksRadioContent;
    List<Track>? _selectedComposerTracks;
    [ObservableProperty] private string _composersRadioContent;
    List<Track>? _selectedConductorTracks;
    [ObservableProperty] private string _remixersRadioContent;
    List<Track>? _selectedRemixerTracks;
    [ObservableProperty] private string _conductorsRadioContent;
    List<Album>? _selectedArtistAlbums;
    [ObservableProperty] private string _albumsRadioContent;
    
    [ObservableProperty] private string? _text;
    
    public Bitmap? Thumbnail => SelectedArtist?.Artwork?.Thumbnail;
    public Artist? _oldArtist;
    private TagsEditorDialog _window;
    public ArtistsEditorViewModel(LibraryViewModel library, TagsEditorDialog window)
    {
        _library = library;
        _window = window;
        _artists = new(library.Data.Artists.Values);
        OnSelectedArtistIndexChanged(0);
    }

    partial void OnPoolFilterChanged(string value)
    {
        _artists.Clear();
        _artists.AddRange(_library.Data.Artists.Values.Where(x => x.Name.Contains(value, StringComparison.OrdinalIgnoreCase)));
        SelectedArtistIndex = -1;
    }

    
    partial void OnSelectedArtistIndexChanged(int value)
    {
        if(_oldArtist != null)
            _oldArtist.Artwork?.ReleaseThumbnail(this);
        
        if (value >= 0 && value < Artists.Count)
        {
            SelectedArtist = _oldArtist =Artists[value];
            Text = SelectedArtist.Name;
            SelectedArtist.Artwork?.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Thumbnail)));
            _selectedArtistTracks = _library.Data.Tracks.Values.Where(x=>x.Artists.Contains(SelectedArtist)).ToList();
            TracksRadioContent = $"Tracks: {_selectedArtistTracks.Count}";
            _selectedComposerTracks = _library.Data.Tracks.Values.Where(x=>x.Composers.Contains(SelectedArtist)).ToList();
            ComposersRadioContent = $"Composers: {_selectedComposerTracks.Count}";
            _selectedRemixerTracks = _library.Data.Tracks.Values.Where(x=>x.Remixer!= null && x.Remixer==SelectedArtist).ToList();
            RemixersRadioContent = $"Remixers: {_selectedRemixerTracks.Count}";
            _selectedConductorTracks = _library.Data.Tracks.Values.Where(x=>x.Conductor!= null && x.Conductor==SelectedArtist).ToList();
            ConductorsRadioContent = $"Conductors: {_selectedConductorTracks.Count}";
            _selectedArtistAlbums = _library.Data.Albums.Values.Where(x=>x.AlbumArtist == SelectedArtist).ToList();
            AlbumsRadioContent = $"Albums: {_selectedArtistAlbums.Count}";
        }
        else
        {
            SelectedArtist = null;
            _selectedArtistTracks = null;
            TracksRadioContent = $"Tracks:";
            _selectedComposerTracks = null;
            ComposersRadioContent = $"Composers:";
            _selectedRemixerTracks = null;
            RemixersRadioContent = $"Remixers:";
            _selectedConductorTracks = null;
            ConductorsRadioContent = $"Conductors:";
            _selectedArtistAlbums = null;
            AlbumsRadioContent = $"Albums:";
        }
        OnSelectedRadioIdxChanged(SelectedRadioIdx);
    }

    partial void OnSelectedRadioIdxChanged(int value)
    {
        switch (value)
        {
            case 0:
                if (_selectedArtistTracks != null)
                    SelectedList = new ObservableCollection<string?>(_selectedArtistTracks.Select(x => x.FilePath));
                break;
            case 1:
                if (_selectedComposerTracks != null)
                    SelectedList = new ObservableCollection<string?>(_selectedComposerTracks.Select(x => x.FilePath));
                break;
            case 2:
                if (_selectedRemixerTracks != null)
                    SelectedList = new ObservableCollection<string?>(_selectedRemixerTracks.Select(x => x.FilePath));
                break;
            case 3:
                if (_selectedConductorTracks != null)
                    SelectedList = new ObservableCollection<string?>(_selectedConductorTracks.Select(x => x.FilePath));
                break;
            case 4:
                if (_selectedArtistAlbums != null)
                    SelectedList = new ObservableCollection<string?>(_selectedArtistAlbums.Select(x => x.Title));
                break;
        }
    }

    public void Dispose()
    {
        if(_oldArtist != null)
            _oldArtist.Artwork?.ReleaseThumbnail(this);
    }

    [RelayCommand]
    void PickArtwork()
    {
        
    }
    [RelayCommand]
    async Task Rename()
    {
        string rename = "unknown artist";
        if(!string.IsNullOrEmpty(Text))
            rename = Text;
        
        var existing = _library.Data.Artists.Values.FirstOrDefault(x => x.Name == rename);
        HashSet<Track> dirtyTracks = new();
        HashSet<Album> dirtyAlbums = new();
        if (existing != null && _selectedArtistTracks != null)
        {
            foreach (var track in _selectedArtistTracks)
            {
                if (track.Artists.Contains(existing)) track.Artists.Remove(SelectedArtist!);
                else track.Artists!.Replace(SelectedArtist, existing);
                dirtyTracks.Add(track);
            }

            if (_selectedComposerTracks != null)
            {
                foreach (var track in _selectedComposerTracks)
                {
                    if (track.Composers.Contains(existing)) track.Composers.Remove(SelectedArtist!);
                    track.Composers!.Replace( SelectedArtist, existing);
                    dirtyTracks.Add(track);
                }
            }

            if (_selectedRemixerTracks != null)
            {
                foreach (var track in _selectedRemixerTracks)
                {
                    track.Remixer = existing;
                    dirtyTracks.Add(track);
                }
            }

            if (_selectedConductorTracks != null)
            {
                foreach (var track in  _selectedConductorTracks)
                {
                    track.Conductor = existing;
                    dirtyTracks.Add(track);
                }
            }

            if (_selectedArtistAlbums != null)
            {
                foreach (var album in _selectedArtistAlbums)
                {
                    album.AlbumArtist = existing;
                    dirtyAlbums.Add(album);
                }
            }

            await UpdateAndReport(dirtyTracks, dirtyAlbums);
            
            
            
            var message = $"{dirtyTracks.Count} tracks updated. \n{dirtyAlbums.Count} albums updated";
            await DialogUtils.MessageBox(_window, "Success", message);
        }
        else
        {
            SelectedArtist!.Name = rename;
            await SelectedArtist.DbUpdateAsync(_library.Database);
            await DialogUtils.MessageBox(_window, "Success", $"Renamed one artist");
        }
    }

    [RelayCommand]
    async Task Delete()
    {
        if (SelectedArtist is null) return;
        HashSet<Track> dirtyTracks = new();
        HashSet<Album> dirtyAlbums = new();
        var unknown = _library.Data.Artists.Values.First(x=>x.Name.Equals("unknown artist"));
        
        foreach (var track in _selectedArtistTracks     )
        {
            if (track.Artists.Count > 1) track.Artists.Remove(SelectedArtist!);
            else track.Artists.Replace(SelectedArtist!, unknown);
            dirtyTracks.Add(track);
        }

        if (_selectedComposerTracks != null)
        {
            foreach (var track in _selectedComposerTracks)
            {
                track.Composers.Remove(SelectedArtist!);
                dirtyTracks.Add(track);    
            }
        }

        if (_selectedRemixerTracks != null)
        {
            foreach (var track in _selectedRemixerTracks)
            {
                track.Remixer = null;
                dirtyTracks.Add(track);
            }
        }

        if (_selectedConductorTracks != null)
        {
            foreach (var track in  _selectedConductorTracks)
            {
                track.Conductor = null;
                dirtyTracks.Add(track);
            }
        }

        if (_selectedArtistAlbums != null)
        {
            foreach (var album in _selectedArtistAlbums)
            {
                album.AlbumArtist = unknown;
                dirtyAlbums.Add(album);
            }
        }

        await UpdateAndReport(dirtyTracks, dirtyAlbums);
        await SelectedArtist.DbDeleteAsync(_library.Database);
        _library.Data.Artists.Remove(SelectedArtist.DatabaseIndex);
            
        var message = $"{dirtyTracks.Count} tracks updated. \n{dirtyAlbums.Count} albums updated";
        await DialogUtils.MessageBox(_window, "Success", message);
    }
    [RelayCommand]
    void ArrowUp()
    {
        var index = SelectedArtistIndex - 1;
        if (index >=Artists.Count || index < 0) index = Artists.Count-1;
        SelectedArtistIndex = index;
    }
    [RelayCommand]
    void ArrowDown()
    {
        var index = SelectedArtistIndex + 1;
        if (index >=Artists.Count || index < 0) index = 0;
        SelectedArtistIndex = index;
    }
    private async Task UpdateAndReport(HashSet<Track> dirtyTracks, HashSet<Album> dirtyAlbums)
    {
        if (dirtyTracks.Count > 0 || dirtyAlbums.Count > 0)
        {
            var progress = new ProgressDialogViewModel();
            Dispatcher.UIThread.Post(() => _ = progress.Show(), DispatcherPriority.Render);
            await progress.DialogShown;
            await Task.Delay(100);
            
            
            List<Task> tasks = new();
            var total = dirtyTracks.Count;
            var current = 0;
            foreach (var track in dirtyTracks)
            {
                tasks.Add(track.DbUpdateAsync(_library.Database));
                tasks.Add(track.UpdateArtistsAsync(_library));
                TagWriter.EnqueueFileUpdate(track);
                
                if (tasks.Count > 8)
                {
                    progress.Progress.Report(("Updating tracks", (double)current/total, false));
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                }
                current++;
            }
            progress.Progress.Report(("Updating tracks", (double)current/total, false));
            await Task.WhenAll(tasks);
            tasks.Clear();
            
            total = dirtyAlbums.Count;
            current = 0;
            foreach (var album in dirtyAlbums)
            {
                tasks.Add(album.DbUpdateAsync(_library.Database));
                if (tasks.Count > 8)
                {
                    progress.Progress.Report(("Updating albums", (double)current/total, false));
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                }
                current++;
            }
            progress.Progress.Report(("Updating albums", (double)current/total, false));
            await Task.WhenAll(tasks);
            progress.Progress.Report(("done", 1, true));
            progress.Dispose();
        }
    }
}