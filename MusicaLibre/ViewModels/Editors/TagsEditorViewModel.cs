using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.Views;

namespace MusicaLibre.ViewModels;

public partial class TagsEditorViewModel:TracksListViewModel
{
    [ObservableProperty] private bool _isMultiple;
    private const string _mult = $"Multiple values";
    public TagsEditorDialog Window { get; set; }
    [ObservableProperty] private GenresEditorViewModel _genresEditor;
    [ObservableProperty] private AlbumsEditorViewModel _albumsEditor;
    [ObservableProperty] private ArtistsEditorViewModel _artistsEditor;
    [ObservableProperty] private PublishersEditorViewModel _publishersEditor;
    [ObservableProperty] private ArtworksEditorViewModel _artworksEditor;
    [ObservableProperty] private TrackArtworkManagerViewModel _selectedTrackArtwork;
    
    //Constructor
    public TagsEditorViewModel(LibraryViewModel library, List<Track> tracksPool, TagsEditorDialog window) : base(library, tracksPool)
    {
        Window = window;
        window.Closing += OnWindowClosing;

        _selectedTrackArtwork = new(this, window);
        _columns = new List<TrackViewColumn>()
        {
            new("Path", TrackSortKeys.FilePath, t => t.Model.FileName, this)
            {
                ToolTipGetter = track => track.Model.FilePath,
            },
        };
        
        UpdateCollection();
        var albums =TracksPool.Select(x=>x.Album).Distinct();
        PoolAlbums = Library.Data.Albums.Values.Where(x=> albums.Contains(x)).ToList();
        
        var discs = TracksPool.Select(x=>(x.DiscNumber, x.AlbumId)).Distinct();
        PoolDiscs = Library.Data.Discs.Values.Where(x=> discs.Contains((x.Number, x.AlbumId))).ToList();
        
        var artists = TracksPool.SelectMany(x => x.Artists).Distinct(); 
        PoolArtists = Library.Data.Artists.Values.Where(x=> artists.Contains(x)).ToList();
        
        var composers = TracksPool.SelectMany(x => x.Composers).Distinct();
        PoolComposers = Library.Data.Artists.Values.Where(x=> composers.Contains(x)).ToList();
        
        var genres = TracksPool.SelectMany(x => x.Genres).Distinct();
        PoolGenres = Library.Data.Genres.Values.Where(x=> genres.Contains(x)).ToList();
        
        InputManager.IsDragSelecting = true;
        foreach (var track in _items)
            track.IsSelected = true;
        InputManager.IsDragSelecting = false;
        
        GenresEditor = new GenresEditorViewModel(Library);
        ArtistsEditor = new ArtistsEditorViewModel(Library, Window);
        
        if(PoolAlbums.Any())
            AlbumsEditor = new AlbumsEditorViewModel(this);
        
        PublishersEditor = new PublishersEditorViewModel(Library, Window);
        ArtworksEditor = new ArtworksEditorViewModel(this);
    }
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        try
        {
            GenresEditor.Dispose();
            ArtistsEditor.Dispose();
            AlbumsEditor.Dispose();    
            PublishersEditor.Dispose();
            ArtworksEditor.Dispose();
        }
        catch (Exception ex) {Console.WriteLine(ex);}
    }


    protected override void SelectedTrackChanged()
    {
        IsMultiple = SelectedTracks?.Count > 1;
        
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(Codec));
        OnPropertyChanged(nameof(Bitrate));
        OnPropertyChanged(nameof(Channels));
        OnPropertyChanged(nameof(SampleRate));
        
        AddedBinding = Added;
        ModifiedBinding = Modified;
        CreatedBinding = Created;
        PlayedBinding = Played;
        RatingBinding = Rating.HasValue ? Rating.Value : 0;
        TitleBinding = Title;
        DiscBinding = DiscNumber;
        TrackNumberBinding = TrackNumber;
        AlbumBinding = Album;
        AlbumArtistBinding = AlbumArtist??string.Empty;
        YearBinding = Year;
        PublisherBinding = Publisher;
        ComposersBinding = Composers;
        ArtistsBinding = Artists;
        GenresBinding = Genres;
        ConductorBinding = Conductor;
        RemixerBinding = Remixer;
        CommentsBinding = Comments;

        SelectedTrackArtwork.SelectedTrack = SelectedItem;
    }

#region TrackTags 
    public string FilePath=>IsMultiple ? CoalescedFilePath : SelectedItem?.Model.FilePath??"";
    public string CoalescedFilePath => 
        Utils.Coalesce(SelectedItems.Select(x=>x.Model.FilePath).ToArray())
        ??_mult;
    
    public string Duration=>IsMultiple ? CoalescedDuration : SelectedItem?.Duration??"";
    public string CoalescedDuration=>
        Utils.Coalesce(SelectedItems.Select(x=>x.Duration).ToArray())
        ??_mult;
    public string Codec=> IsMultiple ? CoalescedCodec : $"{SelectedItem?.Model.Codec}";
    public string CoalescedCodec =>
        Utils.Coalesce(SelectedItems.Select(x=>x.Model.Codec).ToArray())
        ??_mult;
    public string Bitrate=>IsMultiple ? CoalescedBitrate:$"{SelectedItem?.Model.BitrateKbps}Kbps";
    public string CoalescedBitrate =>
        Utils.Coalesce(SelectedItems.Select(x=>$"{x.Bitrate}Kbps").ToArray())
        ??_mult;
    public string Channels=>IsMultiple? CoalescedChannels : $"{SelectedItem?.Model.Channels}";
    public string CoalescedChannels =>
        Utils.Coalesce(SelectedItems.Select(x=>$"{x.Model.Channels}").ToArray())
        ??_mult;
    public string SampleRate=>IsMultiple?CoalescedSampleRate:$"{SelectedItem?.Model.SampleRate??0}Khz";
    public string CoalescedSampleRate =>
        Utils.Coalesce(SelectedItems.Select(x=>$"{x.Model.SampleRate}Khz").ToArray())
        ??_mult;

    //Added time
    [ObservableProperty] private string _addedBinding = string.Empty;
    public string Added => IsMultiple? CoalescedAdded : $"{SelectedItem?.AddedFull}";
    public string CoalescedAdded =>
        Utils.Coalesce(SelectedItems.Select(x=>x.AddedFull).ToArray()) 
        ?? _mult;

    [RelayCommand]
    async Task UpdateAdded()
    {
        var added = TimeUtils.FromDateTimeString(AddedBinding);
        if (added is null)
        {
            await DialogUtils.MessageBox(Window, "Error",
                "Could not parse time format. Correct format is yyyy-MM-dd HH:mm:ss");
            return;
        }
        foreach (var vm in SelectedItems)
        {
            vm.Model.DateAdded = added.Value;
            await vm.Model.DbUpdateAsync(Library.Database);
        }
        await DialogUtils.MessageBox(Window, "Success",
            $"{SelectedItems.Count} file(s) updated successfully");
        SelectedTrackChanged();
    }
    
    //Modified Time
    [ObservableProperty] private string _modifiedBinding = string.Empty;
    public string Modified => IsMultiple ? CoalescedModified : $"{SelectedItem?.ModifiedFull}";

    public string CoalescedModified =>
        Utils.Coalesce(SelectedItems.Select(x => x.ModifiedFull).ToArray()) 
        ??_mult;

    [RelayCommand] async Task UpdateModified()
    {
        var modified = TimeUtils.FromDateTimeString(ModifiedBinding);
        if (modified is null)
        {
            await DialogUtils.MessageBox(Window, "Error",
                "Could not parse time format. Correct format is yyyy-MM-dd HH:mm:ss");
            return;
        }
        foreach (var vm in SelectedItems)
        {
            File.SetLastWriteTime(vm.Model.FilePath, modified.Value);
            vm.Model.Modified = modified.Value;
            await vm.Model.DbUpdateAsync(Library.Database);
        }
        await DialogUtils.MessageBox(Window, "Success",
            $"{SelectedItems.Count} file(s) updated successfully");
        
        SelectedTrackChanged();
    }
    //Created Time
    [ObservableProperty] private string _createdBinding = string.Empty;
    public string Created => IsMultiple ? CoalescedCreated : $"{SelectedItem?.CreatedFull}";
    public string CoalescedCreated =>
        Utils.Coalesce(SelectedItems.Select(x=>x.CreatedFull).ToArray()) 
        ?? _mult;
    [RelayCommand] async Task UpdateCreated()
    {
        var created = TimeUtils.FromDateTimeString(CreatedBinding);
        if (created is null)
        {
            await DialogUtils.MessageBox(Window, "Error",
                "Could not parse time format. Correct format is yyyy-MM-dd HH:mm:ss");
            return;
        }
        foreach (var vm in SelectedItems)
        {
            File.SetCreationTime(vm.Model.FilePath, created.Value);
            vm.Model.Created = created.Value;
            await vm.Model.DbUpdateAsync(Library.Database);
        }
        await DialogUtils.MessageBox(Window, "Success",
            $"{SelectedItems.Count} file(s) updated successfully");
        SelectedTrackChanged();
    }
    //Last Played
    [ObservableProperty] private string _playedBinding = string.Empty;
    public string Played => IsMultiple? CoalescedPlayed : $"{SelectedItem?.Model.LastPlayed}";
    public string CoalescedPlayed =>
        Utils.Coalesce(SelectedItems.Select(x=>x.Model.LastPlayed).ToArray()).ToString()
        ??_mult;
    //Rating
    [ObservableProperty] private double _ratingBinding;
    public double? Rating => SelectedItem?.Model.Rating??0;
    
    
    //Title
    [ObservableProperty] private string _titleBinding = string.Empty;
    public string Title => IsMultiple ? CoalescedTitle : $"{SelectedItem?.Model.Title}";
    public string CoalescedTitle =>
        Utils.Coalesce(SelectedItems.Select(x=>$"{x.Model.Title}").ToArray())
        ??_mult;

    [RelayCommand]
    async Task TitleUpdated()
    {
        if(string.IsNullOrWhiteSpace(TitleBinding) )return;
        if(SelectedItem is null) return;
        
        SelectedItem.Model.Title = TitleBinding;
        await SelectedItem.Model.DbUpdateAsync(Library.Database);
        await DialogUtils.MessageBox(Window, "Success",
            $"{SelectedItem.Model.FilePath} updated successfully");
    }


    //Track Number
    [ObservableProperty] private string _trackNumberBinding = string.Empty;
    public string TrackNumber => IsMultiple ? CoalescedTrackNumber : $"{SelectedItem?.Model.TrackNumber}";
    public string CoalescedTrackNumber =>
        Utils.Coalesce(SelectedItems.Select(x=>$"{x.Model.TrackNumber}").ToArray())
        ??_mult;

    [RelayCommand] async Task UpdateTrackNumber()
    {
        if (uint.TryParse(TrackNumberBinding, out uint number) && SelectedItem != null)
        {
            List<Task> tasks = new List<Task>();
            foreach (var track in SelectedItems.Select(x => x.Model))
            {
                track.TrackNumber = number;
                tasks.Add( track.DbUpdateAsync(Library.Database));
            }
            await Task.WhenAll(tasks);
            await DialogUtils.MessageBox(Window, "Success",
                $"{SelectedItems.Count} database entries updated successfully");
        }
        
    }
    
    //AlbumDisc 
    [ObservableProperty] private string _discBinding = string.Empty;
    public string DiscNumber => IsMultiple ? CoalescedDisc : $"{SelectedItem?.Model.DiscNumber}";
    public string CoalescedDisc =>
        Utils.Coalesce(SelectedItems.Select(x=>$"{x.Model.DiscNumber}").ToArray())
        ??_mult;

    [RelayCommand] async Task UpdateDiscNumber()
    {
        if (!uint.TryParse(DiscBinding, out uint number)) return;
        List<Task> tasks = new List<Task>();
        foreach (var track in SelectedItems)
        {
            if (track.Model.DiscNumber != number)
            {
                track.Model.DiscNumber = number;
                tasks.Add(track.Model.DbUpdateAsync(Library.Database));
            }
        }
        
        foreach (var album in SelectedItems.Select(x => x.Model.Album).Distinct())
        {
            var albumDiscs = Library.Data.Discs.Values.Where(x => x.AlbumId == album.DatabaseIndex).ToList();
            //remove discs that no track references anymore
            var discNumbersInAlbumTracks = Library.Data.Tracks.Values
                .Where(x => x.AlbumId == album.DatabaseIndex)
                .GroupBy(x => x.DiscNumber)
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var disc in albumDiscs)
            {
                if (!discNumbersInAlbumTracks.ContainsKey(disc.Number))
                {
                    tasks.Add(disc.DbDeleteAsync(Library.Database));
                    Library.Data.Discs.Remove((disc.Number, disc.AlbumId));
                }
            }
            
            //Add disc if it doesn't exist yet.
            var existing =  albumDiscs.FirstOrDefault(x => x.Number == number);
            if (existing is null)
            {
                var disc = new Disc(number, album);
                Library.Data.Discs.Add((number, album.DatabaseIndex), disc);
                tasks.Add(disc.DbInsertAsync(Library.Database));
            }
        } 
        await Task.WhenAll(tasks);
        await DialogUtils.MessageBox(Window, "Success", $"{SelectedItems.Count} database entries updated successfully");
    }
    
    //Album
    [ObservableProperty] private string _albumBinding = string.Empty;
    [ObservableProperty] private IEnumerable<string> _albumOptions;
    public string Album => IsMultiple ? CoalescedAlbum : $"{SelectedItem?.Model.Album.Title}";
    public string CoalescedAlbum =>
        Utils.Coalesce(SelectedItems.Select(x=>x.Model.Album.Title).ToArray())
        ??_mult;
    partial void OnAlbumBindingChanged(string value)
    {
        var albums = Library.Data.Albums.Values.Where(x => x.Title.StartsWith(value, StringComparison.OrdinalIgnoreCase));
        AlbumOptions = albums.Select(x=>$"{x.Title}");
    }

    [RelayCommand] async Task AlbumUpdated()
    {
        if(SelectedItems.Count == 0) return;
        
        var title = AlbumBinding;
        
        var oldAlbums = SelectedItems.Select(x=> x.Model.Album).Distinct().ToList();
        ObservableCollection<string>? renames = null;
        try
        {
            renames = new ObservableCollection<string>(
                oldAlbums.Select(x => $"{x.Title} : {x.AlbumArtist.Name} : {x.Year.Name}").ToArray());    
        }
        catch(Exception ex){Console.WriteLine(ex);}
        
        
        
        var existingAlbums = Library.Data.Albums.Values.Where(x => x.Title.Equals(title)).ToList();
        var existings = new ObservableCollection<string>(
            existingAlbums.Select(x => $"{x.Title} : {x.AlbumArtist.Name} : {x.Year.Name}") .ToArray());
        
        
        Album? album;
        var createdAlbumCount = 0;
        var selectionArtist = SelectedItems.SelectMany(x=>x.Model.Artists).GroupBy(a=> a)
            .Select(g => new { Artist = g.Key, Count = g.Count() })
            .OrderBy(x=>x.Count).FirstOrDefault() ?.Artist;
        
        var selectionYear = SelectedItems.GroupBy(x=>x.Model.Year)
            .Select(g => new { Year = g.Key, Count = g.Count() })
            .OrderBy(x=>x.Count).FirstOrDefault()?.Year;
        
        var selectionDirectories = SelectedItems.Select(x=>x.Model.Folder.Name).Distinct().ToList();
        var selectionFolderRoot = PathUtils.GetCommonRoot(selectionDirectories);
        

        var dlgvm = new AlbumUpdatedDialogViewModel()
        {
            Title = "Select Album",
            Content = "Choose Existing Album or Create a new one",
            Existings = existings.Count > 0 ? existings : null,
            Renames = renames?.Count > 0 ? renames : null,
            NewAlbumTitle = title,
            NewAlbumArtist = selectionArtist?.Name?? string.Empty,
            NewAlbumYear = selectionYear?.Name??string.Empty,
            NewAlbumFolder = selectionFolderRoot??string.Empty,
        };
       
        var dlg = new AlbumUpdatedDialog();
        dlg.DataContext = dlgvm;
        if (await dlg.ShowDialog<bool>(Window))
        {
            if (dlgvm.RenameButtonChecked)
            {
                if (renames == null)
                {
                    await DialogUtils.MessageBox(Window,  "Error","Rename button checked but renames is empty" );
                    return;
                }
                var idx = dlgvm.SelectedRenameIndex;
                if (renames.Count == 1) idx = 0;
                
                if (idx >= 0 && idx < renames.Count)
                {
                    album = oldAlbums[idx];
                    album.Title = title;
                    await album.DbUpdateAsync(Library.Database);
                }
                else
                {
                    await DialogUtils.MessageBox(Window,  "Error","selected album index out of range" );
                    return;
                }
            }
            else if (dlgvm.SelectButtonChecked)
            {
                var idx = dlgvm.SelectedExistingIndex;
                if(existingAlbums.Count == 1) idx = 0;

                if (idx >= 0 && idx < existingAlbums.Count)
                {
                    album = existingAlbums[idx];
                }
                else
                {
                    await DialogUtils.MessageBox(Window,  "Error","selected album index out of range" );
                    return;
                }
            }
            else if (dlgvm.CreateButtonChecked)
            {
                var artist = Library.Data.Artists.Values.FirstOrDefault(x => x.Name.Equals(dlgvm.NewAlbumArtist, StringComparison.OrdinalIgnoreCase));
                if (artist == null)
                {
                    artist = new Artist(dlgvm.NewAlbumArtist);
                    await artist.DbInsertAsync(Library.Database);
                    Library.Data.Artists.Add(artist.DatabaseIndex, artist);
                }

                if (!UInt32.TryParse(dlgvm.NewAlbumYear, out uint y))
                {
                    await DialogUtils.MessageBox(Window,  "Error","Invalid Year" );
                    return;
                }
                
                var year = Library.Data.Years.Values.FirstOrDefault(x=> x.Number == y);
                if (year == null)
                {
                    year = new Year(y);
                    await year.DbInsertAsync(Library.Database);
                    Library.Data.Years.Add(year.DatabaseIndex, year);
                }
                
                var folder = Library.Data.Folders.Values.FirstOrDefault(x => x.Name.Equals(dlgvm.NewAlbumFolder));
                if (folder == null)
                {
                    folder = new Folder(dlgvm.NewAlbumFolder);
                    await folder.DbInsertAsync(Library.Database);
                    Library.Data.Folders.Add(folder.DatabaseIndex, folder);
                }

                album = new Album(title, artist, year );
                album.ArtistId = album.AlbumArtist.DatabaseIndex;
                album.YearId = album.Year.DatabaseIndex;
                album.Folder =  folder;
                album.FolderId = album.Folder.DatabaseIndex;
                album.Created = TimeUtils.Earliest(SelectedItems.Select(x=> x.Model.Created));
                album.Modified = TimeUtils.Latest(SelectedItems.Select(x=> x.Model.Modified));
                album.Added = TimeUtils.Earliest(SelectedItems.Select(x=> x.Model.Created));
                album.LastPlayed = null;
                await album.DbInsertAsync(Library.Database);
                Library.Data.Albums.Add(album.DatabaseIndex, album);
                createdAlbumCount++;
            }
            else
            {
                await DialogUtils.MessageBox(Window,  "Error","Invalid Choice, make sure you check one radio button" );
                return;
            }
        }
        else return;        

        int createdDisksCount = 0;

        //assign album to all selected tracks
        foreach (var track in SelectedItems.Where(x => x.Model.Album != album))
        {
            track.Model.AlbumId = album.DatabaseIndex;
            track.Model.Album = album;
            await track.Model.DbUpdateAsync(Library.Database);
            if(! Library.Data.Discs.TryGetValue((track.Model.DiscNumber, album.DatabaseIndex), out var disc))
            {
                disc = new Disc(track.Model.DiscNumber, album);
                disc.AlbumId = album.DatabaseIndex;
                await disc.DbInsertAsync(Library.Database);
                Library.Data.Discs.Add((disc.Number, disc.AlbumId), disc);
                createdDisksCount++;
            }
        }

        int removedAlbumCount = 0;
        //Remove empty albums
        foreach (var oldAlbum in oldAlbums.Where(x => Library.IsAlbumEmpty(x)))
        {
            await Library.RemoveEmptyAlbum(oldAlbum);
            removedAlbumCount++;
        }
        var message=$"{SelectedItems.Count} Tracks updated.";
        if(createdDisksCount!=0)
            message+= $"/n{createdAlbumCount} Album created";
        if(removedAlbumCount !=0)
            message+= $"{removedAlbumCount} Album deleted.";
        if(createdDisksCount!=0)
            message+= $"/n{createdDisksCount} Disks created";
        
        await DialogUtils.MessageBox(Window, "Success",message);
    }
    
    // AlbumArtist
    [ObservableProperty] private string _albumArtistBinding = string.Empty;
    public string? AlbumArtist => IsMultiple ? CoalescedAlbumArtist : SelectedItem?.Model.Album.AlbumArtist.Name;
    public string CoalescedAlbumArtist =>
        Utils.Coalesce(SelectedItems.Select(x => $"{x.Model.Album.AlbumArtist.Name}").ToArray())
        ??_mult;

    [RelayCommand]
    async Task AlbumArtistUpdated()
    {
        var artistName = AlbumArtistBinding;
        if (string.IsNullOrWhiteSpace(artistName)) return;
        int createdArtistCount = 0;
        int updatedAlbumCount = 0;
        var artist = Library.Data.Artists.Values.FirstOrDefault(x => x.Name == artistName);
        if (artist is null)
        {
            artist = new Artist(artistName);
            await artist.DbInsertAsync(Library.Database);
            Library.Data.Artists.Add(artist.DatabaseIndex, artist);
            createdArtistCount++;
        }

        var albumCandidates = Library.Data.Albums.Values
            .Where(x => x.AlbumArtist.Name.Equals(artistName)).ToList();

        List<Task> batch = new();
        foreach (var track in SelectedItems)
        {
            var albumTitle = track.Model.Album.Title;

            var existing = albumCandidates.FirstOrDefault(x => x.Title == albumTitle);
            if (existing is null)
            {
                track.Model.Album.AlbumArtist = artist;
                track.Model.Album.ArtistId = artist.DatabaseIndex;
                batch.Add(track.Model.Album.DbUpdateAsync(Library.Database));
                albumCandidates.Add(track.Model.Album);
                updatedAlbumCount++;
            }
            else
            {
                track.Model.Album = existing;
                batch.Add(track.Model.DbUpdateAsync(Library.Database));
                
            }
        }    
        await Task.WhenAll(batch);
        batch.Clear();
        
        var message=$"{SelectedItems.Count} Tracks updated";
        if(createdArtistCount!=0)
            message+= $"/n{createdArtistCount} Artist created";
        if (updatedAlbumCount!=0)
            message+= $"/n{updatedAlbumCount} Album updated";
        await DialogUtils.MessageBox(Window, "Success",message);
    }

    [RelayCommand]
    void AlbumArtistFromTrack()
    {
        if(SelectedItem is null) return;
        var selectedArtist = SelectedItem.Model.Artists.FirstOrDefault();
        if (selectedArtist is not null)
            AlbumArtistBinding = selectedArtist.Name;
    }
    
    //Year
    [ObservableProperty] private string _yearBinding = string.Empty;
    public string Year => IsMultiple ? CoalescedYear : $"{SelectedItem?.Model.Year.Number}";
    public string CoalescedYear =>
        Utils.Coalesce(SelectedItems.Select(x=>$"{x.Model.Year.Number}").ToArray())
        ??_mult;
    public string CoalescedAlbumYear =>
        Utils.Coalesce(SelectedItems.Select(x=>$"{x.Model.Album.Year.Number}").ToArray())
        ??_mult;

    [RelayCommand]
    async Task YearUpdated()
    {
        if(! uint.TryParse(YearBinding, out uint y))
            y = 0;
        
        int yearCreatedCount = 0;
        var year = Library.Data.Years.Values.FirstOrDefault(x => x.Number.Equals(y));
        if (year is null || year.DatabaseIndex == 0)
        {
            year = new Year(y);
            await year.DbInsertAsync(Library.Database);
            Library.Data.Years.Add(year.DatabaseIndex, year);
            yearCreatedCount++;
        }
        
        foreach (var track in SelectedItems.Where(x=>x.Model.Year != year))
        {
            track.Model.Year = year;
            await track.Model.DbUpdateAsync(Library.Database);
        }
        
        var message=$"{SelectedItems.Count} tracks updated";
        if(yearCreatedCount!=0)
            message+= $"/n{yearCreatedCount} Years created";
        await DialogUtils.MessageBox(Window, "Success", message);
    }
    [RelayCommand]
    async Task UpdateAlbumYear()
    {
        if(! uint.TryParse(YearBinding, out uint y))
            y = 0;
        
        var year = Library.Data.Years.Values.FirstOrDefault(x => x.Number.Equals(y));
        if (year is null || year.DatabaseIndex == 0)
        {
            year = new Year(y);
            await year.DbInsertAsync(Library.Database);
            Library.Data.Years.Add(year.DatabaseIndex, year);
        }
        int albumUpdatedCount = 0;
        foreach (var album in SelectedItems.Select(x => x.Model.Album).Distinct())
        {
            if (album.Year != year)
            {
                album.Year = year;
                await album.DbUpdateAsync(Library.Database);
                albumUpdatedCount++;
            }
        }
        OnPropertyChanged(nameof(CoalescedAlbumYear));
        await DialogUtils.MessageBox(Window, "Success", $"{albumUpdatedCount} albums updated");
    }

    //Publisher
    [ObservableProperty] private string _publisherBinding = string.Empty;
    [ObservableProperty] private IEnumerable<string> _publisherOptions;
    public string Publisher => IsMultiple ? CoalescedPublisher : $"{SelectedItem?.Model.Publisher?.Name}";
    public string CoalescedPublisher =>
        Utils.Coalesce(SelectedItems.Select(x=>x.Model.Publisher?.Name).ToArray())
        ??_mult;
    partial void OnPublisherBindingChanged(string value)
    {
        PublisherOptions= Library.Data.Publishers.Values
            .Where(x => x.Name.StartsWith(value, StringComparison.OrdinalIgnoreCase))
            .Select(x=>$"{x.Name}");
    }

    [RelayCommand]
    async Task PublisherUpdated()
    {
        if (string.IsNullOrWhiteSpace(PublisherBinding)) return;
        var publisher = Library.Data.Publishers.Values
            .FirstOrDefault(x => x.Name.Equals(PublisherBinding, StringComparison.OrdinalIgnoreCase));
        int publisherCreatedCount = 0;
        if (publisher is null)
        {
            publisher = new Publisher(PublisherBinding);
            var folderpath = Path.Combine(Library.Path, Library.Settings.PublisherArtworkPath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var publisherart = Library.Data.Artworks.Values
                .Where(x => x.Folder.Name.Equals(folderpath));
            var artwork = publisherart
                .FirstOrDefault(x=> x.SourcePath.Contains(publisher.Name, StringComparison.OrdinalIgnoreCase));
            if (artwork is not null)
            {
                publisher.Artwork = artwork;
                publisher.ArtworkId = artwork.DatabaseIndex;
            }
            await publisher.DbInsertAsync(Library.Database);
            
            Library.Data.Publishers.Add(publisher.DatabaseIndex, publisher);
            publisherCreatedCount++;
        }
        foreach (var track in SelectedItems.Where(x => x.Model.Publisher != publisher))
        {
            track.Model.Publisher = publisher;
            await track.Model.DbUpdateAsync(Library.Database);
        }
        var message=$"{SelectedItems.Count} tracks updated";
        if (publisherCreatedCount!=0)
            message+= $"/n{publisherCreatedCount} Publisher created";
        await DialogUtils.MessageBox(Window, "Success", message);
    }

    //Artists
    [ObservableProperty] private string _artistsBinding = string.Empty;
    [ObservableProperty] private IEnumerable<string> _artistsOptions;
    public string Artists => IsMultiple ? CoalescedArtists : $"{SelectedItem?.Artists}";    
    public string CoalescedArtists =>
        Utils.Coalesce(SelectedItems.Select(x=>x.Artists).ToArray())
        ??_mult;

    partial void OnArtistsBindingChanged(string value)
    {
        if(string.IsNullOrWhiteSpace(value))return;
        var splits = value.Split(',').Select(x => x.Trim());
        var current = splits.LastOrDefault();
        if (current is null) return;
        
        ArtistsOptions= Library.Data.Artists.Values
            .Where(x => x.Name.StartsWith(current, StringComparison.OrdinalIgnoreCase))
            .Select(x=>$"{x.Name}");
    }

    [RelayCommand]
    async Task ArtistsUpdated()
    {
        if(string.IsNullOrWhiteSpace(ArtistsBinding))return;
        var splits = ArtistsBinding.Split(',').Select(x => x.Trim());
        var artists=new List<Artist>();
        int artistCreatedCount = 0;
        foreach (var split in splits)
        {
            var artist = Library.Data.Artists.Values
                .FirstOrDefault(x => x.Name.Equals(split, StringComparison.OrdinalIgnoreCase));
            if (artist is null)
            {
                artist = new Artist(split);
                await artist.DbInsertAsync(Library.Database);
                Library.Data.Artists.Add(artist.DatabaseIndex, artist);
                artistCreatedCount++;
            }
            artists.Add(artist);
        }

        foreach (var track in SelectedItems)
        {
            track.Model.Artists.Clear();
            track.Model.Artists.AddRange(artists);
            await track.Model.UpdateArtistsAsync(Library);
        }
        var message=$"{SelectedItems.Count} Tracks updated";
        if (artistCreatedCount!=0)
            message+= $"/n{artistCreatedCount} Artist created";
        await DialogUtils.MessageBox(Window, "Success", message);
    }

    //Genres
    [ObservableProperty] private string _genresBinding = string.Empty;
    [ObservableProperty] private IEnumerable<string>? _genreOptions;
    public string Genres => IsMultiple ? CoalescedGenres : $"{SelectedItem?.Genres}";
    public string CoalescedGenres =>
        Utils.Coalesce(SelectedItems.Select(x=>x.Genres).ToArray())
        ??_mult;
    partial void OnGenresBindingChanged(string value)
    {
        var split = value.Split(',');
        var current = split[split.Length - 1].Trim();
        if (!string.IsNullOrEmpty(current))
        {
            GenreOptions = Library.Data.Genres.Values
                .Where(x => x.Name.StartsWith(current, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name);
        }
        else GenreOptions = null;
    }
    [RelayCommand]
    async Task GenreUpdated()
    {
        if(string.IsNullOrWhiteSpace(GenresBinding)) return;
        var splits = GenresBinding.Split(',').Select(x=>x.Trim());
        var genres = new List<Genre>();
        int  genreCreatedCount = 0;
        foreach (var split in splits)
        {
            var genre = Library.Data.Genres.Values.FirstOrDefault(x => x.Name.Equals(split, StringComparison.OrdinalIgnoreCase));
            if (genre == null)
            {
                genre = new Genre(split);
                genre.DbInsert(Library.Database);
                Library.Data.Genres.Add(genre.DatabaseIndex, genre);
                genreCreatedCount++;
            }
            genres.Add(genre);
        }
        foreach ( var track in SelectedItems)
        {
            track.Model.Genres.Clear();
            track.Model.Genres.AddRange(genres);
            await track.Model.UpdateGenresAsync(Library);
        }
        var message=$"{SelectedItems.Count} Tracks updated";
        if (genreCreatedCount!=0)
            message+= $"/n{genreCreatedCount} Genre created";
        await DialogUtils.MessageBox(Window, "Success", message);
    }
    
    //Composers
    [ObservableProperty] private string _composersBinding = string.Empty;
    [ObservableProperty] private IEnumerable<string>? _composersOptions;
    public string Composers => IsMultiple? CoalescedComposers : $"{SelectedItem?.Composers}";
    public string CoalescedComposers =>
        Utils.Coalesce(SelectedItems.Select(x=>x.Composers).ToArray())
        ??_mult;

    partial void OnComposersBindingChanged(string value)
    {
        
        var splits = value.Split(',').Select(x => x.Trim());
        var current = splits.LastOrDefault();
        if (!string.IsNullOrEmpty(current))
        {
            ComposersOptions = Library.Data.Artists.Values
                .Where(x => x.Name.StartsWith(current, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name);
        }
        else ComposersOptions = null;
    }

    [RelayCommand]
    async Task ComposersUpdated()
    {
        var splits = ComposersBinding.Split(',').Select(x => x.Trim());
        var composers = new List<Artist>();
        foreach (var split in splits)
        {
            var artist = Library.Data.Artists.Values.FirstOrDefault(x => x.Name.Equals(split, StringComparison.OrdinalIgnoreCase));
            if (artist is null)
            {
                artist = new Artist(split);
                await artist.DbInsertAsync(Library.Database);
                Library.Data.Artists.Add(artist.DatabaseIndex, artist);
            }
            composers.Add(artist);
        }
        foreach (var item in SelectedItems)
        {
            item.Model.Composers = composers;
            await item.Model.UpdateArtistsAsync(Library);
        }
        await DialogUtils.MessageBox(Window, "Success", $"{SelectedItems.Count} tracks updated with {composers.Count} composers");
    }
    
    //Remixers
    [ObservableProperty] private string _remixerBinding = string.Empty;
    [ObservableProperty] private IEnumerable<string>? _remixerOptions;
    public string Remixer => IsMultiple? CoalescedRemixer : $"{SelectedItem?.Model.Remixer?.Name}";
    public string CoalescedRemixer =>
        Utils.Coalesce(SelectedItems.Select(x=>x.Model.Remixer?.Name).ToArray())
        ??_mult;

    partial void OnRemixerBindingChanged(string value)
    {
        var current = value.Trim();
        if (!string.IsNullOrEmpty(current))
        {
            RemixerOptions = Library.Data.Artists.Values
                .Where(x => x.Name.StartsWith(current, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name);
        }
        else RemixerOptions = null;
    }

    [RelayCommand]
    async Task RemixerUpdated()
    {
        Artist? artist = null;
        var name = RemixerBinding.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            artist = Library.Data.Artists.Values.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (artist is null)
            {
                artist = new Artist(name);
                await artist.DbInsertAsync(Library.Database);
                Library.Data.Artists.Add(artist.DatabaseIndex, artist);
            }
        }
        foreach (var item in SelectedItems)
        {
            item.Model.Remixer = artist;
            item.Model.RemixerId = artist?.DatabaseIndex;
            await item.Model.DbUpdateAsync(Library.Database);
        }
        await DialogUtils.MessageBox(Window, "Success", $"{SelectedItems.Count} tracks updated");
    }
    
    //Conductor
    [ObservableProperty] private string _conductorBinding = string.Empty;
    [ObservableProperty] private IEnumerable<string>? _conductorOptions;
    public string Conductor => IsMultiple ? CoalescedConductor : $"{SelectedItem?.Model.Conductor?.Name}";
    public string CoalescedConductor =>
        Utils.Coalesce(SelectedItems.Select(x=>$"{x.Model.Conductor?.Name}").ToArray())
        ??_mult;

    partial void OnConductorBindingChanged(string value)
    {
        var current = value.Trim();
        if (!string.IsNullOrEmpty(current))
        {
            ConductorOptions = Library.Data.Artists.Values
                .Where(x => x.Name.StartsWith(current, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name);
        }
        else RemixerOptions = null;
    }

    [RelayCommand]
    async Task ConductorUpdated()
    {
        Artist? artist = null;
        var name = ConductorBinding.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            artist = Library.Data.Artists.Values.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (artist is null)
            {
                artist = new Artist(name);
                await artist.DbInsertAsync(Library.Database);
                Library.Data.Artists.Add(artist.DatabaseIndex, artist);
            }
        }
        foreach (var item in SelectedItems)
        {
            item.Model.Conductor = artist;
            item.Model.ConductorId = artist?.DatabaseIndex;
            await item.Model.DbUpdateAsync(Library.Database);
        }
        await DialogUtils.MessageBox(Window, "Success", $"{SelectedItems.Count} tracks updated");        
    }
    
    // Comments
    [ObservableProperty]private string _commentsBinding = string.Empty;
    public string Comments => IsMultiple ? CoalescedComments : $"{SelectedItem?.Model.Comment}";
    public string CoalescedComments =>
        Utils.Coalesce(SelectedItems.Select(x=> x.Model.Comment).ToArray())
        ?? _mult;

    [RelayCommand]
    async Task CommentsUpdated()
    {
        foreach (var item in SelectedItems)
        {
            item.Model.Comment = CommentsBinding;
            await item.Model.DbUpdateAsync(Library.Database);
        }
        await DialogUtils.MessageBox(Window, "Success", $"{SelectedItems.Count} tracks updated");
    }
 #endregion   
 
    public List<Album> PoolAlbums; 
    public List<Disc> PoolDiscs;
    public List<Genre> PoolGenres;
    
    public List<Artist> PoolArtists;
    public List<Artist> PoolComposers;

    public List<Album>? SelectionAlbums =>(SelectedTracks == null || SelectedTracks.Count == 0) ? null :
        PoolAlbums.Where(p=>SelectedTracks.Select(t=>t.Album).Contains(p)).ToList();

    public List<Disc>? SelectionDiscs=>(SelectedTracks == null || SelectedTracks.Count == 0) ? null :
        PoolDiscs.Where(p=>SelectedTracks.Select(t=>(t.DiscNumber, t.AlbumId)).Contains((p.Number, p.AlbumId))).ToList();
    
    public List<Genre>? SelectionGenres=>(SelectedTracks == null || SelectedTracks.Count == 0) ? null :
        PoolGenres.Where(p=>SelectedTracks.SelectMany(t=>t.Genres).Contains(p)).ToList();
        
    public List<Artist>? SelectionArtists=>(SelectedTracks == null || SelectedTracks.Count == 0) ? null :
        PoolArtists.Where(p=>SelectedTracks.SelectMany(t=>t.Artists).Contains(p)).ToList();

    public List<Artist>? SelectionComposers => (SelectedTracks == null || SelectedTracks.Count == 0) ? null :
        PoolComposers.Where(p=>SelectedTracks.SelectMany(t=>t.Artists).Contains(p)).ToList();







    [RelayCommand]
    void ModifiedFromBackup()
    {
        string backupRoot = "/media/Data/Musique Copy/musique/";
        string root = "/media/Data/musique";
        if(SelectedTracks is not null )
        {
            foreach (var track in SelectedTracks)
            {
                var relativePath = Path.GetRelativePath(root, track.FilePath);
                var backupPath = Path.Combine(backupRoot, relativePath);
                var created = File.GetCreationTime(backupPath);
                var modified = File.GetLastWriteTime(backupPath);
                File.SetCreationTime(track.FilePath, created);
                track.Created = created;
                File.SetLastWriteTime(track.FilePath, modified);
                track.Modified = modified;
                
                _ = track.DbUpdateAsync(Library.Database);
            }
        }
    }
    [RelayCommand] async Task RefreshTimeStamps()
    {
        if(SelectedTracks is not null )
        {
            foreach (var track in SelectedTracks)
            {
                track.Created = File.GetCreationTime(track.FilePath);
                track.Modified = File.GetLastWriteTime(track.FilePath);
                
                await track.DbUpdateAsync(Library.Database);
                    
                OnPropertyChanged(CreatedBinding);
                OnPropertyChanged(ModifiedBinding);
            }
        }
    }

    [RelayCommand]
    async Task UpdateFilesTags()
    {
        foreach (var item in SelectedItems)
        {
            TagWriter.EnqueueFileUpdate(item.Model);
        }
        await DialogUtils.MessageBox(Window, "Success", $"{SelectedItems.Count} files updated");
    }
}

