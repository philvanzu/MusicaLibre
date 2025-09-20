using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;
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
    public DbSyncManager DbSyncManager { get; init; }

    private readonly object _dataLock = new();
    private volatile LibrarySnapshot _data = new LibrarySnapshot();
    public LibrarySnapshot Data
    {
        get { lock (_dataLock) return _data; }
        private set
        {
            lock (_dataLock) 
            {
                _data = value;
                Refresh();
            }
        }
    }


    private int _currentStepIndex;

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
    [ObservableProperty] private string _searchString=string.Empty;
    //partial void OnSearchStringChanged(string value)=>DataPresenter.Filter(value);
    
    public LibraryViewModel(Database db, string libraryRoot, MainWindowViewModel mainWindowViewModel)
    {
        MainWindowViewModel = mainWindowViewModel;

        Name = System.IO.Path.GetFileName(libraryRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        Path = libraryRoot;
        Database = db;
        DbSyncManager = new DbSyncManager(this);
        Settings = LibrarySettingsViewModel.Load(Database);
        Settings.PropertyChanged += (_, args) =>
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
        Data.Populate(Database); // load the whole db in memory except the blobs.
        Database.SetModeAsync(true).Wait();
        DbSyncManager.StartSyncService();
        CurrentOrdering = Settings.CustomOrderings[Settings.SelectedOrdering];
        OrderingChanged();
        AppData.Instance.AppState.CurrentLibrary = Database.Path;
        AppData.Instance.Save();
    }
    
    public void Close()
    {
        Settings.Save(Database);
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

    void Refresh()
    {
        if(_navigator == null) return;
        var navstack = _navigator.ToList();
        var pool = Data.Tracks.Values.ToList();
        var idx = _currentStepIndex;
        _currentStepIndex = 0;
        foreach (var presenter in navstack)
        {
            if (pool == null) break;
            presenter.TracksPool = pool;
            _currentStepIndex++;
            pool = presenter.SelectedTracks;
        }
        _currentStepIndex = idx;
    }

    void OrderingStepChanged()
    {
        var capsule = DataPresenter?.GetCapsule();
        lock (_dataLock)
        {
            List<Track> pool = Navigator?.Current?.SelectedTracks ?? Data.Tracks.Values.ToList();
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
            }    
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
        DataPresenter?.Filter(SearchString);
    }
    [RelayCommand]
    void ClearSearch()
    {
        SearchString = string.Empty;
        Search();
    }

    public async Task EditTracks(List<Track>? tracks)
    {
        // wait sync process to finish if running
        if (DbSyncManager.SyncTask != null) 
            await DbSyncManager.SyncTask;
        
        // set flag in DbSyncManager to delay sync process starting as long as dialog is open
        await DbSyncManager.SyncLock.WaitAsync();
        try
        {
            if (tracks == null || tracks.Count == 0) return;
            var dialog = new TagsEditorDialog();
            var vm = new TagsEditorViewModel(this, tracks, dialog);
            InputManager.Instance.Attach(dialog);
            dialog.DataContext = vm;
            await dialog.ShowDialog(MainWindowViewModel.MainWindow);
        }
        finally
        {
            DbSyncManager.SyncLock.Release();
        }
    }
}



