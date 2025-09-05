using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;
using MusicaLibre.Views;
using TagLib;
using File = System.IO.File;

namespace MusicaLibre.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _path;
    [ObservableProperty] private LibrarySettingsViewModel _settings;
    [ObservableProperty] private LibraryDataPresenter? _dataPresenter;
    [ObservableProperty] private LibraryViewModel? _selectedLibrary;
    [ObservableProperty] private NowPlayingListViewModel _nowPlayingList;
    [ObservableProperty] private NavCapsuleViewModel? _capsule;
    partial void OnCapsuleChanged(NavCapsuleViewModel? oldValue, NavCapsuleViewModel? newValue)
    {
        if (oldValue != null) oldValue.Release();
        if (newValue != null ) newValue.Register();
    }

    public MainWindowViewModel MainWindowViewModel { get; init; }
    public Database Database { get; init; }

    public Dictionary<long, Track> Tracks { get; set; } = new();
    public Dictionary<long, Album> Albums { get; set; } = new();
    public Dictionary<long, Disc> Discs { get; set; } = new();
    public Dictionary<long, Artist> Artists { get; set; } = new();
    public Dictionary<long, Genre> Genres { get; set; } = new();
    public Dictionary<long, Publisher> Publishers { get; set; } = new();
    public Dictionary<long, AudioFormat> AudioFormats { get; set; } = new();
    public Dictionary<long, Artwork> Artworks { get; set; } = new();
    public Dictionary<long, Playlist> Playlists { get; set; } = new();
    public Dictionary<long, Year> Years { get; set; } = new();
    public Dictionary<long, Folder> Folders { get; set; } = new();

    private int _currentStepIndex = 0;

    private CustomOrdering CurrentOrdering { get; set; } = new();
    public OrderingStep CurrentStep {
        get
        {
            if(_currentStepIndex == CurrentOrdering.Steps.Count)
                return CurrentOrdering.TracksStep;
            
            return CurrentOrdering.Steps[_currentStepIndex];           
        }
}
       

    [ObservableProperty] private NavigatorViewModel<LibraryDataPresenter>? _navigator;
    [ObservableProperty] private string _searchString;
    partial void OnSearchStringChanged(string value)
    {
        
    }

    public LibraryViewModel(Database db, string libraryRoot, MainWindowViewModel mainWindowViewModel)
    {
        MainWindowViewModel = mainWindowViewModel;

        Name = System.IO.Path.GetFileName(libraryRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        Path = libraryRoot;
        Database = db;
        Settings = LibrarySettingsViewModel.Load(Database);
        Settings.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(Settings.SelectedOrdering))
            {
               OrderingChanged();
            }
        };

        _nowPlayingList = new NowPlayingListViewModel(this, new List<Track>());
    }

    public static LibraryViewModel? Create(DirectoryInfo libraryRoot, MainWindowViewModel mainWindowViewModel)
    {
        try
        {
            var db = Database.Create(libraryRoot);
            if( db==null ) throw new Exception("Could not create Database");
            return new LibraryViewModel(db, libraryRoot.FullName, mainWindowViewModel);
        }
        catch(Exception ex){Console.WriteLine(ex);}

        return null;
    }
    public static LibraryViewModel? Load(FileInfo dbPathInfo, MainWindowViewModel mainWindowViewModel)
    {
        var path = dbPathInfo.FullName;
        if (File.Exists(path))
        {
            var db = new Database(path);
            var sql = $"Select * from Info;";
            var rows = db.ExecuteReader(sql);
            if (rows.Count < 1 || rows.Count > 1)
                throw new CorruptFileException($"Corrupt library database at {path} : multiple Info rows.");

            var dbRoot = Database.GetString(rows[0], "LibraryRoot");
            var dbPath = Database.GetString(rows[0], "DBPath");
            if (dbPath != path)
                throw new CorruptFileException($"Corrupt library database at {path} : dbPath mismatch");

            if (!string.IsNullOrWhiteSpace(dbRoot))
                return new LibraryViewModel(db, dbRoot, mainWindowViewModel);
        }

        return null;
    }

    public void Open()
    {
        Populate(); // load the whole db in memory except the blobs.
        Database.SetModeAsync(true).Wait();
        CurrentOrdering = Settings.CustomOrderings[Settings.SelectedOrdering];
        OrderingChanged();
        AppData.Instance.AppState.CurrentLibrary = Database.Path;
        AppData.Instance.Save();
    }
    
    public void Close()
    {
        Settings.Save(Database);
    }

    public void Populate()
    {
        Database.Open();
        try
        {
            Genres = Genre.FromDatabase(Database);
            Genres.Add(0, Genre.Null);
            Publishers = Publisher.FromDatabase(Database);
            Publishers.Add(0, Publisher.Null);
            Artists = Artist.FromDatabase(Database);
            Artists.Add(0, Artist.Null);
            Tracks = Track.FromDatabase(Database);
            AudioFormats = AudioFormat.FromDatabase(Database);
            AudioFormats.Add(0, AudioFormat.Null);
            Artworks = Artwork.FromDatabase(Database);
            Albums = Album.FromDatabase(Database);
            Albums.Add(0, Album.Null);
            Discs = Disc.FromDatabase(Database);
            Discs.Add(0, Disc.Null);
            Playlists = Playlist.FromDatabase(Database);
            Years = Year.FromDatabase(Database);
            Years.Add(0, Year.Null);
            Folders = Folder.FromDatabase(Database);
            Folders.Add(0, Folder.Null);

            //Resolve all foreign keys
            foreach (var track in Tracks.Values)
            {
                if(track.AlbumId != null) track.Album = Albums[track.AlbumId.Value];
                else track.Album = Albums[0];
                
                if(track.DiscId != null) track.Disc = Discs[track.DiscId.Value];
                else track.Disc = Discs[0];
                
                if(track.PublisherId != null) track.Publisher = Publishers[track.PublisherId.Value];
                else track.Publisher = Publishers[0];
                
                if(track.ConductorId != null) track.Conductor = Artists[track.ConductorId.Value];
                else track.Conductor = Artists[0];
                
                if(track.RemixerId != null) track.Remixer = Artists[track.RemixerId.Value];
                else track.Remixer = Artists[0];
                
                if (track.AudioFormatId != null) track.AudioFormat = AudioFormats[track.AudioFormatId.Value];
                else track.AudioFormat = AudioFormats[0];
                
                if(track.YearId != null) track.Year = Years[track.YearId.Value];
                else track.Year = Years[0];
                
                if(track.FolderId != null) track.Folder = Folders[track.FolderId.Value];
                else track.Folder = Folders[0];
            }
            foreach (var album in Albums.Values)
            {
                if(album.FolderId != null) album.Folder = Folders[album.FolderId.Value];
                else album.Folder = Folders[0];
                
                if(album.ArtistId != null) album.AlbumArtist = Artists[album.ArtistId.Value];
                else album.AlbumArtist = Artists[0];
                
                if(album.YearId != null) album.Year = Years[album.YearId.Value];
                else album.Year = Years[0];
                
                if(album.CoverId != null) album.Cover = Artworks[album.CoverId.Value];
            }

            foreach (var disc in Discs.Values)
                if(disc.AlbumId > 0) disc.Album = Albums[disc.AlbumId];
                else disc.Album = Albums[0];

            foreach (var artwork in Artworks.Values)
            {
                if(artwork.FolderId != null) artwork.Folder = Folders[artwork.FolderId.Value];
                else artwork.Folder = Folders[0];
            }
            foreach (var playlist in Playlists.Values)
            {
                if(playlist.FolderId != null) playlist.Folder = Folders[playlist.FolderId.Value];
                else playlist.Folder = Folders[0];
            }
            
            //Resolve all many to many relationships
            var sql = "Select * from TrackGenres";
            foreach (var row in Database.ExecuteReader(sql))
            {
                var trackId = Database.GetValue<long>(row, "TrackId");
                var genreId = Database.GetValue<long>(row, "GenreId");
                Tracks[trackId!.Value].Genres.Add(Genres[genreId!.Value]);    
            }
            sql  = "Select * from TrackArtists";
            foreach (var row in Database.ExecuteReader(sql))
            {
                var trackId = Database.GetValue<long>(row, "TrackId");
                var artistId = Database.GetValue<long>(row, "ArtistId");
                Tracks[trackId!.Value].Artists.Add(Artists[artistId!.Value]);    
            }
            sql  = "Select * from TrackComposers";
            foreach (var row in Database.ExecuteReader(sql))
            {
                var trackId = Database.GetValue<long>(row, "TrackId");
                var artistId = Database.GetValue<long>(row, "ArtistId");
                Tracks[trackId!.Value].Composers.Add(Artists[artistId!.Value]);    
            }
            sql  = "Select * from TrackArtworks";
            foreach (var row in Database.ExecuteReader(sql))
            {
                var trackId = Database.GetValue<long>(row, "TrackId");
                var artworkId = Database.GetValue<long>(row, "ArtworkId");
                Tracks[trackId!.Value].Artworks.Add(Artworks[artworkId!.Value]);    
            }
            sql =  "Select * from AlbumArtworks";
            foreach (var row in Database.ExecuteReader(sql))
            {
                var albumId = Database.GetValue<long>(row, "AlbumId");
                var artworkId = Database.GetValue<long>(row, "ArtworkId");
                Albums[albumId!.Value].Artworks.Add(Artworks[artworkId!.Value]);    
            }
            sql = "Select * from PlaylistTracks";
            foreach (var row in Database.ExecuteReader(sql))
            {
                var playlistId = Convert.ToInt64(row["PlaylistId"]);
                var trackId = Convert.ToInt64(row["TrackId"]);
                var position =  Convert.ToInt32(row["Position"]);
                Playlists[playlistId].Tracks.Add((Tracks[trackId],position));
            }

            //foreach (var playlist in Playlists.ToList())
            //    if(playlist.Value.Tracks.Count == 0) Playlists.Remove(playlist.Key);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            Database.Close();
        }
    }

    void OrderingChanged()
    {
        CurrentOrdering = Settings.CustomOrderings[Settings.SelectedOrdering];
        _currentStepIndex = 0;
        Navigator= null;
        DataPresenter = null;
        OrderingStepChanged();
    }
    
    public void ChangeOrderingStep(LibraryDataPresenter presenter)
    {
        var idx = _currentStepIndex + 1;
        if (idx >= 0 && idx <= CurrentOrdering.Steps.Count)
        {
            _currentStepIndex = idx;
            OrderingStepChanged();
        }
    }
    [RelayCommand]
    void NavigateBack()
    {
        if (Navigator == null || !Navigator.CanGoBack) return;
        if (_currentStepIndex > 0)
        {
            Navigator.GoBack();
            _currentStepIndex--;
            if (Navigator.Current != null)
            {
                DataPresenter = Navigator.Current;
                Capsule = DataPresenter.PreviousCapsule;
            }
            else OrderingStepChanged();
        }
    }

    void OrderingStepChanged()
    {
        List<Track> pool = Navigator?.Current?.SelectedTracks ?? Tracks.Values.ToList();
        var capsule = DataPresenter?.GetCapsule();
        switch (CurrentStep.Type)
        {
            case OrderGroupingType.Album:
                DataPresenter = new AlbumsListViewModel(this, pool);
                break;
            case OrderGroupingType.Disc :
                DataPresenter = new DiscsListViewModel(this, pool);
                break;
            case OrderGroupingType.Artist:
                DataPresenter = new ArtistsListViewModel(this, pool);
                break;
            case OrderGroupingType.Playlist: 
                DataPresenter = new PlaylistsListViewModel(this, pool);
                break;
            case OrderGroupingType.Track:
                var list = new TracksListViewModel(this, pool);
                list.UpdateCollection();
                list.SetCapsule(DataPresenter?.GetCapsule());
                DataPresenter = list;
                break;
            case OrderGroupingType.Year:
                DataPresenter = new YearsListViewModel(this, pool);
                break;
            case OrderGroupingType.Genre:
                DataPresenter = new GenresListViewModel(this, pool);
                break;
            case OrderGroupingType.Publisher:
                DataPresenter = new PublishersListViewModel(this, pool);
                break;
            case OrderGroupingType.Remixer:
                DataPresenter = new RemixersListViewModel(this, pool);
                break;
            case OrderGroupingType.Composer:
                DataPresenter = new ComposersListViewModel(this, pool);
                break;
            case OrderGroupingType.Conductor:
                DataPresenter = new ConductorsListViewModel(this, pool);
                break;
            case OrderGroupingType.Folder:
                DataPresenter = new FoldersListViewModel(this, pool);
                break;
            case OrderGroupingType.Bitrate_Format:
                DataPresenter = new AudioFormatsListViewModel(this, pool);
                break;
            default: break;
        }

        if (DataPresenter != null)
        {
            
            Capsule = capsule ?? new NavCapsuleViewModel()
            {
                Title = $"{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Name)}",
                SubTitle = "Library",
                
                Artwork = null
            };
            Capsule.CurrentOrderingText = CurrentStep.ToString();
            DataPresenter.PreviousCapsule = Capsule;
            
            if (Navigator == null)
                Navigator = new NavigatorViewModel<LibraryDataPresenter>(this, DataPresenter);
            else
                Navigator.Navigate(DataPresenter);
            
            
        }
    }

    [RelayCommand]
    async Task ManageCustomOrderings()
    {
        var dialog = new OrderingEditorDialog();
        var dialogVm = new OrderingEditorViewModel(this,  dialog);
        
        dialog.DataContext = dialogVm;
        await dialog.ShowDialog(MainWindowViewModel.MainWindow);
        Settings.RefreshOrderings();
    }

    [RelayCommand]
    void Search()
    {
        
    }
    [RelayCommand]
    void ClearSearch()
    {
        SearchString = string.Empty;
    }
}



