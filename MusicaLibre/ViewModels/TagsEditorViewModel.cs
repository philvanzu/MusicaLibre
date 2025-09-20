using System;
using System.Collections.Generic;
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

    
    //Constructor
    public TagsEditorViewModel(LibraryViewModel library, List<Track> tracksPool, TagsEditorDialog window) : base(library, tracksPool)
    {
        Window = window;
        window.Closing += OnWindowClosing;
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
        AlbumsEditor = new AlbumsEditorViewModel(this, PoolAlbums);
    }
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        GenresEditor.Dispose();
        AlbumsEditor.Dispose();
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
        YearBinding = Year;
        PublisherBinding = Publisher;
        ComposersBinding = Composers;
        ArtistsBinding = Artists;
        GenresBinding = Genres;
        ConductorBinding = Conductor;
        RemixerBinding = Remixer;
        
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

    [ObservableProperty] private string _addedBinding = string.Empty;
    public string Added => IsMultiple? CoalescedAdded : $"{SelectedItem?.Model.DateAdded}";
    public string CoalescedAdded =>
        Utils.Coalesce(SelectedItems.Select(x=>x.Model.DateAdded.ToString()).ToArray()) 
        ?? _mult;
    [ObservableProperty] private string _modifiedBinding = string.Empty;
    public string Modified => IsMultiple ? CoalescedModified : $"{SelectedItem?.Model.Modified}";

    public string CoalescedModified =>
        Utils.Coalesce(SelectedItems.Select(x => x.Model.Modified.ToString()).ToArray()) 
        ??_mult;

    [ObservableProperty] private string _createdBinding = string.Empty;
    public string Created => IsMultiple ? CoalescedCreated : $"{SelectedItem?.Model.Created}";
    public string CoalescedCreated =>
        Utils.Coalesce(SelectedItems.Select(x=>x.Model.Created.ToString()).ToArray()) 
        ?? _mult;

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
        TagWriter.EnqueueFileUpdate(SelectedItem.Model);
    }


    //Track Number
    [ObservableProperty] private string _trackNumberBinding = string.Empty;
    public string TrackNumber => IsMultiple ? CoalescedTrackNumber : $"{SelectedItem?.Model.TrackNumber}";
    public string CoalescedTrackNumber =>
        Utils.Coalesce(SelectedItems.Select(x=>$"{x.Model.TrackNumber}").ToArray())
        ??_mult;

    [RelayCommand] void UpdateTrackNumber()
    {
        if (uint.TryParse(TrackNumberBinding, out uint number) && SelectedItem != null)
        {
            foreach (var track in SelectedItems.Select(x => x.Model))
            {
                track.TrackNumber = number;
                _ = track.DbUpdateAsync(Library.Database);
                TagWriter.EnqueueFileUpdate(track);
            }
        }
    }
    
    //AlbumDisc 
    [ObservableProperty] private string _discBinding = string.Empty;
    public string DiscNumber => IsMultiple ? CoalescedDisc : $"{SelectedItem?.Model.DiscNumber}";
    public string CoalescedDisc =>
        Utils.Coalesce(SelectedItems.Select(x=>$"{x.Model.DiscNumber}").ToArray())
        ??_mult;

    [RelayCommand] void UpdateDiscNumber()
    {
        if (!uint.TryParse(DiscBinding, out uint number)) return;
        if (SelectedTracks == null) return;
        foreach (var track in SelectedTracks)
        {
            if (track.DiscNumber != number)
            {
                track.DiscNumber = number;
                _ = track.DbUpdateAsync(Library.Database);
                TagWriter.EnqueueFileUpdate(track);
            }
        }

        foreach (var album in SelectedTracks.Select(x => x.Album).Distinct())
        {
            if (album == null) continue;
            var albumDiscs = Library.Data.Discs.Values.Where(x => x.AlbumId == album.DatabaseIndex).ToList();
            //remove discs that no track references anymore
            var discNumbersInAlbumTracks = Library.Data.Tracks.Values
                .Where(x => x.AlbumId == album.DatabaseIndex)
                .Select(x => x.DiscNumber).Distinct().ToArray();
            foreach (var disc in albumDiscs)
                if (!discNumbersInAlbumTracks.Contains(disc.Number)) 
                    _library.RemoveDisc(disc);
            
            //Add disc if it doesn't exist yet.
            var existing =  albumDiscs.FirstOrDefault(x => x.Number == number);
            if (existing is null)
                Library.AddDisc(number, album);
        }    
    }
    
    //Album
    [ObservableProperty] private string _albumBinding = string.Empty;
    [ObservableProperty] private IEnumerable<string> _albumOptions;
    public string Album => IsMultiple ? CoalescedAlbum : $"{SelectedItem?.Model.Album?.Title}";
    public string CoalescedAlbum =>
        Utils.Coalesce(SelectedItems.Select(x=>x.Model.Album?.Title).ToArray())
        ??_mult;
    partial void OnAlbumBindingChanged(string value)
    {
        var albums = Library.Data.Albums.Values.Where(x => x.Title.StartsWith(value, StringComparison.OrdinalIgnoreCase));
        AlbumOptions = albums.Select(x=>$"{x.Title}");
    }

    [RelayCommand] async Task AlbumUpdated()
    {
        var title = AlbumBinding;
        var oldAlbums = SelectedTracks?.Select(x=> x.Album).Distinct().ToList();
        var album = Library.Data.Albums.Values.FirstOrDefault(x => x.Title == title);

        if (album is null || album.DatabaseIndex == 0) // new title doesn't exist yet
        {
            album = SelectedItem?.Model.Album;
            if (album is not null)
            {
                album.Title = title; //rename selected track's album to title input
                album.Year = SelectedItem?.Model.Year;
                album.Folder = SelectedItem?.Model.Folder??Library.Data.Folders[0];
                await  album.DbUpdateAsync(Library.Database);
            }
        }
        
        //assign album to all selected tracks
        if ( album is not null && SelectedTracks is not null)
        {
            foreach (var track in SelectedTracks.Where(x => x.Album != album))
            {
                track.AlbumId = album.DatabaseIndex;
                track.Album = album;
                _ = track.DbUpdateAsync(Library.Database);
                TagWriter.EnqueueFileUpdate(track);    
            }
        }
        
        //Remove empty albums
        if(oldAlbums != null )
            foreach(var oldAlbum in oldAlbums
                        .Where(x => x!= null && Library.IsAlbumEmpty(x)))
                Library.RemoveEmptyAlbum(oldAlbum!);
    }
    
    //Year
    [ObservableProperty] private string _yearBinding = string.Empty;
    public string Year => IsMultiple ? CoalescedYear : $"{SelectedItem?.Model.Year?.Number}";
    public string CoalescedYear =>
        Utils.Coalesce(SelectedItems.Select(x=>$"{x.Model.Year?.Number}").ToArray())
        ??_mult;

    [RelayCommand]
    async Task YearUpdated()
    {
        if(string.IsNullOrWhiteSpace(YearBinding))return;
        if (uint.TryParse(YearBinding, out uint y))
        {
            var year = Library.Data.Years.Values.FirstOrDefault(x => x.Number.Equals(y));
            if (year is null || year.DatabaseIndex == 0)
            {
                year = new Year(y);
                await year.DbInsertAsync(Library.Database);
                Library.Data.Years.Add(year.DatabaseIndex, year);
            }
            if(SelectedTracks is not null)
            {
                foreach (var track in SelectedTracks.Where(x=>x.Year != year))
                {
                    track.Year = year;
                    await track.DbUpdateAsync(Library.Database);
                    TagWriter.EnqueueFileUpdate(track);
                }
            }
        }
        
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

        if (publisher is null)
        {
            publisher = new Publisher(PublisherBinding);
            await publisher.DbInsertAsync(Library.Database);
            Library.Data.Publishers.Add(publisher.DatabaseIndex, publisher);
        }
        if(SelectedTracks is not null)
        {
            foreach (var track in SelectedTracks.Where(x => x.Publisher != publisher))
            {
                track.Publisher = publisher;
                await track.DbUpdateAsync(Library.Database);
                TagWriter.EnqueueFileUpdate(track);
            }
        }
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
        foreach (var split in splits)
        {
            var artist = Library.Data.Artists.Values
                .FirstOrDefault(x => x.Name.Equals(split, StringComparison.OrdinalIgnoreCase));
            if (artist is null)
            {
                artist = new Artist(split);
                await artist.DbInsertAsync(Library.Database);
                Library.Data.Artists.Add(artist.DatabaseIndex, artist);
            }
            artists.Add(artist);
        }
        if(SelectedTracks is not null)
        {
            foreach (var track in SelectedTracks)
            {
                track.Artists.Clear();
                track.Artists.AddRange(artists);
                await track.UpdateArtistsAsync(Library);
            }
        }
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
        if(string.IsNullOrWhiteSpace(GenresBinding) || SelectedTracks == null) return;
        var splits = GenresBinding.Split(',').Select(x=>x.Trim());
        var genres = new List<Genre>();
        foreach (var split in splits)
        {
            var genre = Library.Data.Genres.Values.FirstOrDefault(x => x.Name.Equals(split, StringComparison.OrdinalIgnoreCase));
            if (genre == null)
            {
                genre = new Genre(split);
                genre.DbInsert(Library.Database);
                Library.Data.Genres.Add(genre.DatabaseIndex, genre);
            }
            genres.Add(genre);
        }
        foreach ( var track in SelectedTracks)
        {
            track.Genres.Clear();
            track.Genres.AddRange(genres);
            await track.UpdateGenresAsync(Library);
        }
    }
    
    //Composers
    [ObservableProperty] private string _composersBinding = string.Empty;
    public string Composers => IsMultiple? CoalescedComposers : $"{SelectedItem?.Composers}";
    public string CoalescedComposers =>
        Utils.Coalesce(SelectedItems.Select(x=>x.Composers).ToArray())
        ??_mult;
    
    [ObservableProperty] private string _remixerBinding = string.Empty;
    public string Remixer => IsMultiple? CoalescedRemixer : $"{SelectedItem?.Model.Remixer?.Name}";
    public string CoalescedRemixer =>
        Utils.Coalesce(SelectedItems.Select(x=>x.Model.Remixer?.Name).ToArray())
        ??_mult;

    [ObservableProperty] private string _conductorBinding = string.Empty;
    public string Conductor => IsMultiple ? CoalescedConductor : $"{SelectedItem?.Model.Conductor?.Name}";
    public string CoalescedConductor =>
        Utils.Coalesce(SelectedItems.Select(x=>$"{x.Model.Conductor?.Name}").ToArray())
        ??_mult;
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



    
}

