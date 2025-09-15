namespace MusicaLibre.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Models;
using Services;

public partial class LibraryViewModel
{
    private const string _unknownArtistStr = "unknown artist";
    #region Populate Database
    
    public async Task ParseLibraryRecursiveAsync(  string path,
                                                    ProgressViewModel progress,
                                                    SemaphoreSlim throttler,
                                                    Action<FileInfo>? addTrack, 
                                                    Action<FileInfo>? addImage,
                                                    Action<FileInfo>? addPlaylist,
                                                    Action<string>? addFolder)
    {
        if (!Directory.Exists(path)) return;
        //Console.WriteLine($"Processing {path}");
        var cancellationToken =  progress.CancellationTokenSource!.Token;
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
        
            progress.Counter = progress.Counter + 1;
            progress.Progress.Report(($"Loaded Directories : {progress.Counter}", -1.0, false));
            var dirInfo = new DirectoryInfo(path);
            addFolder?.Invoke(dirInfo.FullName);
            var subDirs = dirInfo.GetDirectories();
            var files = dirInfo.GetFiles();
            
            // Analyze files
            foreach (var file in files)
            {
                try
                {
                    await throttler.WaitAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (PathUtils.IsAudioFile(file.Extension))
                    {
                        addTrack?.Invoke(file);
                    }
                    else if (PathUtils.IsImage(file.Extension))
                    {
                        addImage?.Invoke(file);
                    }
                    else if (PathUtils.IsPlaylist(file.Extension))
                    {
                        addPlaylist?.Invoke(file);
                    }    
                }
                catch (Exception ex){Console.WriteLine(ex);}
                finally{ throttler.Release(); }
                
            }
            
            // Recursively parse subdirectories
            foreach (var subDir in subDirs)
            {
                
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await ParseLibraryRecursiveAsync(subDir.FullName, progress, throttler, addTrack, addImage, addPlaylist, addFolder);                    
                }
                catch (OperationCanceledException) { }
            }
        }
        catch (OperationCanceledException) {  }
        catch (Exception ex){Console.WriteLine(ex);}
        
    }
    
    public async Task CreateLibrary(string path, ProgressViewModel progress)
    {
        if (progress.CancellationTokenSource != null) return;
        
        progress.CancellationTokenSource = new CancellationTokenSource();
        var token = progress.CancellationTokenSource.Token; 
        TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;
        Dictionary<string, Track> tracks = new Dictionary<string, Track>();
        Dictionary<(string, string),Album> albums = new Dictionary<(string, string), Album>();
        Dictionary<(Album, uint), Disc> discs = new Dictionary<(Album, uint), Disc>();
        Dictionary<string, Artist> artists = new Dictionary<string, Artist>();
        Dictionary<string, Genre> genres = new Dictionary<string, Genre>();
        Dictionary<string, Publisher> publishers = new Dictionary<string, Publisher>();
        Dictionary<string, AudioFormat> audioFormats = new Dictionary<string, AudioFormat>();
        Dictionary<string, Artwork> artworks = new Dictionary<string, Artwork>();
        Dictionary<string, Playlist> playlists = new Dictionary<string, Playlist>();
        Dictionary<uint, Year> years = new Dictionary<uint, Year>();
        Dictionary<string, Folder> folders = new Dictionary<string, Folder>();
        List<Artwork> artworkFiles = new List<Artwork>();
        using var throttler = new SemaphoreSlim(4);
        try
        {
            
            await ParseLibraryRecursiveAsync(path, progress, throttler,
                (audioFile) =>
                {
                    //Audio Files
                    tracks.Add(audioFile.FullName, new Track()
                    {
                        FilePath = audioFile.FullName,
                        FileName = audioFile.Name,
                        FolderPathstr = audioFile.DirectoryName??Path,
                        FileExtension = audioFile.Extension,
                        Modified = audioFile.LastWriteTime,
                        Created = audioFile.CreationTime,
                        LastPlayed = audioFile.LastAccessTime,
                        DateAdded = Settings.LibCreationAddedDateSource switch
                        {
                            LibrarySettingsViewModel.LibCreationAddedDateSources.fromCreated 
                                => audioFile.CreationTime,
                            LibrarySettingsViewModel.LibCreationAddedDateSources.fromModified
                                => audioFile.LastWriteTime,
                            _ => DateTime.Now
                        },
                    });
                },
                (imageFile) =>
                {
                    //Image Files
                    var artwork = new Artwork(Database)
                    {
                        SourcePath = imageFile.FullName,
                        FolderPathstr = imageFile.DirectoryName??Path,
                        SourceType = ArtworkSourceType.External,
                        MimeType = PathUtils.GetMimeType(imageFile.Extension)??imageFile.Extension,
                        Role = null,
                        BookletPage = null
                    };
                    
                    artworkFiles.Add(artwork);
                },
                (playlistFile) =>
                {
                    //Playlists Files
                    var playlist = new Playlist()
                    {
                        FilePath = playlistFile.FullName,
                        FileName = playlistFile.Name,
                        FolderPathstr = playlistFile.DirectoryName??Path,
                        Modified = playlistFile.LastWriteTime,
                        Created = playlistFile.CreationTime,
                    };
                    playlists.Add(playlist.FilePath, playlist);
                },
                (folderpath) =>
                {
                    //Folders
                    folders.Add(folderpath, new Folder(folderpath));
                });

            //Processing Folders
            Console.WriteLine("Processing Library data");
            Database.Open();
            Database.BeginTransaction();
            try
            {
                foreach (var folder in folders.Values)
                {
                    try
                    {
                        folder.DbInsert(Database);
                        Debug.Assert(folder.DatabaseIndex > 0);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }

                Database.Commit();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Database.Rollback();
            }
            
            //Processing Image Files
            Database.BeginTransaction();
            try
            {
                progress.Counter = 0;
                int total = artworkFiles.Count;
                foreach (var artwork in artworkFiles)
                {
                    progress.Counter = progress.Counter + 1;
                    progress.Progress.Report(($"Processing images : {progress.Counter}/{total}",
                        (double)progress.Counter / total, false));
                    
                    artwork.Folder = folders[artwork.FolderPathstr];
                    artwork.ProcessImage();
                    
                    if (string.IsNullOrEmpty(artwork.Hash) ) continue;
                    if (!artworks.ContainsKey(artwork.Hash))
                    {
                        artwork.FindRole();
                        try
                        {
                            artwork.DbInsert(Database);
                            artworks.Add(artwork.Hash, artwork);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                        finally
                        {
                            artwork.ThumbnailData = null;
                        }
                    }

                    if (Database.TransactionCounter > 1000)
                    {
                        Database.Commit();
                        Database.BeginTransaction();
                    }
                }

                Database.Commit();
                artworkFiles.Clear();
            }
            catch (Exception ex)
            {
                Database.Rollback();
                Console.WriteLine(ex);
            }
            
            //Processing Audio Files
            Database.BeginTransaction();
            try
            {
                progress.Counter = 0;
                var total = tracks.Count;
                foreach (var track in tracks.Values)
                {
                    token.ThrowIfCancellationRequested();
                    progress.Counter = progress.Counter + 1;
                    progress.Progress.Report(($"Processing Tracks : {progress.Counter}/{total}",
                        (double)progress.Counter / total, false));
                    // get metadata
                    try
                    {
                        track.Folder = folders[track.FolderPathstr];
                        
                        using var file = TagLib.File.Create(track.FilePath);
                        track.Title = file.Tag.Title??"";
                        


                        track.TrackNumber = file.Tag.Track;
                        track.DiscNumber = file.Tag.Disc;
                        track.Comment = file.Tag.Comment??String.Empty;

                        track.Duration = file.Properties.Duration;
                        track.BitrateKbps = file.Properties.AudioBitrate;
                        track.SampleRate = file.Properties.AudioSampleRate;
                        track.Channels = file.Properties.AudioChannels;
                        try
                        {
                            var codecs = file.Properties.Codecs.Where(x=>x != null).Select(c => c.Description);
                            track.Codec = string.Join(", ", codecs);    
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                            track.Codec = track.FileExtension;
                        }
                        
                        
                        var y = file.Tag.Year;
                        if (!years.TryGetValue(y, out var year))
                        {
                            year = new Year(y);
                            years.Add(y, year);
                            try
                            {
                                year.DbInsert(Database);
                                Debug.Assert(year.DatabaseIndex > 0);
                            }
                            catch (Exception ex){Console.WriteLine(ex);}
                        }
                        track.Year = year;
                        
                        var format = AudioFormat.Generate(track.Codec, track.BitrateKbps);
                        if (!audioFormats.TryGetValue(format, out var audioFormat))
                        {
                            audioFormat = new AudioFormat(format);
                            audioFormats.Add(format, audioFormat);
                            try
                            {
                                audioFormat.DbInsert(Database);
                                Debug.Assert(audioFormat.DatabaseIndex > 0);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex);
                            }
                        }
                        track.AudioFormat = audioFormat;
                        track.AudioFormatId = audioFormat.DatabaseIndex;
                        
                        var performers = file.Tag.Performers?? Array.Empty<string>();
                        
                        var composers = file.Tag.Composers?? Array.Empty<string>();
                        performers = performers.Concat(composers).ToArray();
                        
                        var albumPerformer = string.Join(" & ", file.Tag.AlbumArtists).Trim();
                        performers = performers.Concat(new[] { albumPerformer }).ToArray();
                        
                        var conductor = file.Tag.Conductor;
                        if (!string.IsNullOrWhiteSpace(conductor))
                        {
                            conductor = conductor.Trim();
                            performers = performers.Concat(new[] { conductor }).ToArray();
                        }
                        
                        var remixer = file.Tag.RemixedBy;
                        if (!string.IsNullOrWhiteSpace(remixer))
                        {
                            remixer = remixer.Trim();
                            performers = performers.Concat(new[] { remixer }).ToArray();
                        }

                        if (!artists.TryGetValue(_unknownArtistStr, out var unknownArtist))
                        {
                            try
                            {
                                unknownArtist = new Artist(_unknownArtistStr);
                                artists.Add(_unknownArtistStr, unknownArtist);
                                unknownArtist.DbInsert(Database);    
                                Debug.Assert(unknownArtist.DatabaseIndex > 0);
                            }
                            catch (Exception ex){Console.WriteLine(ex);}
                            
                        }
                        Artist albumArtist = unknownArtist!;
                        
                        foreach (var str in performers)
                        {
                            var performer = str.Trim();
                            if (string.IsNullOrWhiteSpace(performer))
                                performer = _unknownArtistStr;
                            if(string.IsNullOrWhiteSpace(albumPerformer))
                                albumPerformer = performer;
                            if (!artists.TryGetValue(performer, out var artist))
                            {
                                try
                                {
                                    artist = new Artist(performer);
                                    artist.DbInsert(Database);
                                    Debug.Assert(artist.DatabaseIndex > 0);
                                    artists.Add(performer, artist);    
                                }
                                catch (Exception ex){ Console.WriteLine(ex); }
                            }

                            if (artist != null)
                            {
                                track.Artists.Add(artist);
                                if(artist.Name.Equals(albumPerformer, StringComparison.Ordinal))
                                    albumArtist = artist;
                                if(artist.Name.Equals(conductor, StringComparison.Ordinal))
                                    track.Conductor = artist;
                                if(artist.Name.Equals(remixer, StringComparison.Ordinal))
                                    track.Remixer = artist;
                                if(composers.Contains(artist.Name))
                                    track.Composers.Add(artist);
                            }
                        }
                        
                        if (file.Tag.Album != null)
                        {
                            if (albumArtist == unknownArtist 
                                && artists.TryGetValue(albumPerformer, out var artist))
                                albumArtist = artist;
    
                            albums.TryGetValue((file.Tag.Album, albumPerformer), out var album);
                            if (album == null)
                            {
                                album = new Album(file.Tag.Album, albumArtist, track.Year);
                                album.Folder = track.Folder;
                                albums.Add((file.Tag.Album, albumPerformer), album);
                                try
                                {
                                    album.DbInsert(Database);
                                    Debug.Assert(album.DatabaseIndex > 0);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }
                            }
                            
                            
                            track.Album = album;    
                            if(!discs.TryGetValue((album, track.DiscNumber), out var disc))
                            {
                                disc = new Disc(track.DiscNumber, album);
                                discs.Add((album, track.DiscNumber), disc);
                                try
                                {
                                    disc.DbInsert(Database);
                                    Debug.Assert(disc.DatabaseIndex > 0);
                                }
                                catch (Exception ex){Console.WriteLine(ex);}
                            }

                            int pictureIndex = 0;
                            foreach (var picture in file.Tag.Pictures)
                            {
                                var artwork = new Artwork(Database)
                                {
                                    SourcePath = track.FilePath,
                                    FolderPathstr = track.FolderPathstr,
                                    Folder = track.Folder,
                                    SourceType = ArtworkSourceType.Embedded,
                                    MimeType = picture.MimeType,
                                    Role = ArtworkRole.CoverFront,
                                    EmbedIdx = pictureIndex,
                                };
                                artwork.ProcessImage(new MemoryStream(picture.Data.Data));
                                if (string.IsNullOrEmpty(artwork.Hash))
                                {
                                    Console.WriteLine($"Failed to process embedded picture in track {track.FilePath}");
                                    continue;
                                }
                                
                                if (artworks.TryGetValue(artwork.Hash, out var existing))
                                    artwork =  existing;
                                else
                                {
                                    try
                                    {
                                        artwork.DbInsert(Database);
                                        Debug.Assert(artwork.DatabaseIndex > 0);
                                        artworks.Add(artwork.Hash, artwork);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex);
                                    }
                                    finally
                                    {
                                        artwork.ThumbnailData = null;
                                    }
                                    
                                }

                                if(track.Album != null)
                                    track.Album.Artworks.Add(artwork);
                                track.Artworks.Add(artwork);
 
                                
                                pictureIndex++;
                            }
                        }

                        var gs = new List<string>();
                        foreach (var g in file.Tag.Genres ?? Array.Empty<string>())
                        {
                            var split = g.Split(new[] { ",", ";", "/", "|", "\\", " - " }, StringSplitOptions.RemoveEmptyEntries); // trim whitespace around pieces
                            gs.AddRange(split);
                        }
                        foreach (var g in gs)
                        {
                            var gclean = Regex.Replace(g.Trim(), @"\s+", " ");
                            gclean = Regex.Replace(gclean, @"\s*([_&-])\s*", "$1");
                            gclean = Regex.Replace(gclean, @"\s*([&])\s*", " $1 ");
                            gclean = textInfo.ToTitleCase(gclean.ToLower());

                            foreach (var kv in GenreManager.prefixMap)
                            {
                                if (gclean.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                                    gclean = gclean.Replace(kv.Key, kv.Value);
                            }
                            foreach (var kv in GenreManager.acronymsMap)
                            {
                                if (gclean.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                                    gclean = gclean.Replace(kv.Key, kv.Value);
                            }
                                
                            if (GenreManager.canonicalMap.TryGetValue(gclean, out var canonical))
                                gclean = canonical;
                            
                            if (!genres.TryGetValue(gclean, out var genre))
                            {
                                try
                                {
                                    genre = new Genre(gclean);
                                    genre.DbInsert(Database);
                                    Debug.Assert(genre.DatabaseIndex > 0);
                                    genres.Add(gclean, genre);
                                }
                                catch (Exception ex){ Console.WriteLine(ex); }
                                
                            }
                            if( genre != null)
                                track.Genres.Add(genre);
                        }

                        if (!string.IsNullOrEmpty(file.Tag.Publisher))
                        {
                            if (!publishers.TryGetValue(file.Tag.Publisher, out var publisher))
                            {
                                try
                                {
                                    publisher = new Publisher(file.Tag.Publisher);
                                    publisher.DbInsert(Database);
                                    Debug.Assert(publisher.DatabaseIndex > 0);
                                    publishers.Add(file.Tag.Publisher, publisher);
                                }
                                catch (Exception ex){ Console.WriteLine(ex); }
                            }
                            if(publisher != null)
                                track.Publisher = publisher;
                        }


                        try
                        {
                            
                            track.DbInsert(Database);
                            Debug.Assert(track.DatabaseIndex > 0);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                            continue;
                        }
                        
                        foreach (var genre in track.Genres)
                        {
                            var sql = @"INSERT OR IGNORE INTO TrackGenres (TrackId, GenreId) VALUES ($trackid, $genreid);
                                        SELECT last_insert_rowid();";
                            try
                            {
                                Database.ExecuteScalar(sql, new()
                                {
                                    ["$trackid"] = track.DatabaseIndex,
                                    ["$genreid"] = genre.DatabaseIndex,
                                });    
                            }
                            catch (Exception ex){Console.WriteLine($"{ex} || sql query : {sql}");}
                        }

                        foreach (var artist in track.Artists)
                        {
                            var sql = @"INSERT OR IGNORE INTO TrackArtists (TrackId, ArtistId) VALUES ($trackid, $artistid);
                                        SELECT last_insert_rowid();";
                            try
                            {
                                Database.ExecuteScalar(sql, new()
                                {
                                    ["$trackid"] = track.DatabaseIndex,
                                    ["$artistid"] = artist.DatabaseIndex,
                                });    
                            }
                            catch (Exception ex){Console.WriteLine($"{ex} || sql query : {sql}");}
                            
                        }

                        foreach (var composer in track.Composers)
                        {
                            var sql = @"INSERT OR IGNORE INTO TrackComposers (TrackId, ArtistId) VALUES ($trackid, $artistid);
                                        SELECT last_insert_rowid();";
                            try
                            {
                                Database.ExecuteScalar(sql, new()
                                {
                                    ["$trackid"] = track.DatabaseIndex,
                                    ["$artistid"] = composer.DatabaseIndex,
                                });    
                            }
                            catch (Exception ex){Console.WriteLine($"{ex} || sql query : {sql}");}
                        }

                        foreach (var artwork in track.Artworks)
                        {
                            var sql = @"INSERT OR IGNORE INTO TrackArtworks (TrackId, ArtworkId) VALUES ($trackid, $artworkid);
                                        SELECT last_insert_rowid();";
                            try
                            {
                                Database.ExecuteScalar(sql, new()
                                {
                                    ["$trackid"] = track.DatabaseIndex,
                                    ["$artworkid"] = artwork.DatabaseIndex,
                                });    
                            }
                            catch (Exception ex){Console.WriteLine($"{ex} || sql query : {sql}");}
                            
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not open {track.FilePath} with Taglib. {ex}");
                    }
                    if (Database.TransactionCounter > 1000)
                    {
                        Database.Commit();
                        Database.BeginTransaction();
                    }
                }


                Database.Commit();
            }
            catch (Exception ex)
            {
                Database.Rollback();
                Console.WriteLine(ex.ToString());
            }
            
            // Completing Albums Info.
            Database.BeginTransaction();
            try
            {
                progress.Counter = 0;
                var total = albums.Count;
                foreach (var album in albums.Values)
                {
                    progress.Counter++;
                    progress.Progress.Report(($"Processing Albums : {progress.Counter}/{total}",
                        (double)progress.Counter / total, false));

                    HashSet<string> albumfolders = new HashSet<string>();
                    var albumtracks = tracks.Values.Where(x => x.Album == album);

                    DateTime modified = DateTime.MinValue;
                    DateTime created = DateTime.MaxValue;
                    HashSet<string> albumartists = new HashSet<string>();
                    foreach (var track in albumtracks)
                    {
                        albumfolders.Add(track.FolderPathstr);
                        if (modified < track.Modified) modified = track.Modified;
                        if (created > track.Created) created = track.Created;
                        if (album.AlbumArtist == null)
                            foreach (var artist in track.Artists)
                                albumartists.Add(artist.Name);
                    }

                    if (album.AlbumArtist == null && albumartists.Count > 0)
                    {
                        if (albumartists.Count > 2)
                        {
                            if (artists.TryGetValue("Various Artists", out var various))
                                album.AlbumArtist = various;
                            else
                            {
                                various = new Artist("Various Artists");
                                artists.Add("Various Artists", various);
                                album.AlbumArtist = various;
                            }
                        }
                        else
                        {
                            string artistName = string.Join(" & ", albumartists);
                            if (artists.TryGetValue(artistName, out var artist))
                                album.AlbumArtist = artist;
                            else
                            {
                                artist = new Artist(artistName);
                                album.AlbumArtist = artist;
                                artists.Add(artistName, artist);
                            }
                        }
                    }

                    album.Added = DateTime.Now;
                    album.Modified = modified;
                    album.Created = created;

                    
                    var rootFolder = albumfolders.Count switch
                    {
                        1 => null,
                        > 1 =>  PathUtils.GetCommonRoot(albumfolders),
                        _ => folders.First().Value.Name
                    };

                    Folder? rootf = null;
                    if(!string.IsNullOrEmpty(rootFolder))
                    {
                        try
                        {
                            rootf = folders[rootFolder];
                        }
                        catch (Exception ex) { Console.WriteLine(ex); }
                    }
                    
                    if (rootf != null)
                        album.Folder = rootf;

                    var albumArtworks = artworks.Values.Where(x =>
                        x.FolderPathstr.StartsWith(album.Folder.Name, StringComparison.Ordinal)).ToList();
                    if (albumArtworks.Count < 100)
                    {
                        foreach (var artwork in albumArtworks)
                            album.AddArtwork(artwork);
                    }
                    else
                    {
                        Console.WriteLine($"Too many artworks for album {album.Title}");
                    }
                    album.FindAlbumCover();

                    try
                    {
                        album.DbUpdate(Database);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine( $"Failed to update album in database {album.Title} : {ex}");
                    }
                    
                    foreach (var artwork in album.Artworks)
                    {
                        var artworkId = artwork.DatabaseIndex;
                        
                        var sql = @"INSERT OR IGNORE INTO AlbumArtworks (AlbumId, ArtworkId) VALUES ($albumid, $artworkid);";
                        try
                        {
                            Database.ExecuteNonQuery(sql, new()
                            {
                                ["$albumid"] = album.DatabaseIndex,
                                ["$artworkid"] = artworkId,
                            });    
                        }
                        catch (Exception ex){Console.WriteLine($"{ex} || sql query : {sql}");}
                    }
                }
                Database.Commit();
            }
            catch (Exception ex)
            {
                Database.Rollback();
                Console.WriteLine(ex.ToString());
            }
            
            
            //Processing Playlists
            Database.BeginTransaction();
            try
            {
                progress.Counter = 0;
                var total = playlists.Count;
                foreach (var playlist in playlists.Values)
                {
                    progress.Counter++;
                    progress.Progress.Report(($"Processing Playlists : {progress.Counter}/{total}",
                        (double)progress.Counter / total, false));
                    playlist.Folder = folders[playlist.FolderPathstr];
                    bool gotsheetback=false;
                    var list = Playlist.Load(playlist.FilePath, (sheet) =>
                        // Cue Sheets Handler
                    {
                        gotsheetback = true;
                        var performers = new List<string>() { sheet.Performer };
                        performers.AddRange(sheet.Tracks.Select(x => x.Performer));
                        foreach (var performer in performers
                                     .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                        {
                            if (string.IsNullOrWhiteSpace(performer)) continue;
                            if (!artists.TryGetValue(performer, out var artist))
                            {
                                artist = new Artist(performer);
                                artists.Add(performer, artist);
                                try
                                {
                                    artist.DbInsert(Database);
                                    Debug.Assert(artist.DatabaseIndex > 0);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }
                            }
                        }

                        bool createdAlbum = false;
                        if(! albums.TryGetValue((sheet.Title, sheet.Performer), out var album))
                        {
                            album = new Album()
                            {
                                Folder = playlist.Folder,
                                Title = sheet.Title,
                                AlbumArtist = artists[sheet.Performer],
                                Cover = Artworks.Values.FirstOrDefault(x=>x.Folder == playlist.Folder)
                            };
                            createdAlbum = true;
                        }
                        
                        
                        List<Track> sheetTracks = new List<Track>();
                        HashSet<Track> sheetMultiTracks = new HashSet<Track>();
                        foreach (var cuetrack in sheet.Tracks)
                        {
                            if (tracks.TryGetValue(cuetrack.File, out var multitrack))
                            {
                                sheetMultiTracks.Add(multitrack);
                                var track = Track.Copy(multitrack);
                                if(createdAlbum && album.Year is null)
                                    album.Year = track.Year;    
                                track.Title = cuetrack.Title;
                                track.TrackNumber = cuetrack.Number;
                                track.Album = album;
                                
                                if (!string.IsNullOrWhiteSpace(cuetrack.Performer) &&
                                    artists.TryGetValue(cuetrack.Performer, out var artist))
                                {
                                    if(!track.Artists.Contains(artist))
                                        track.Artists.Add( artist);    
                                }
                                var times = TimeUtils.GetCueTrackTimes(cuetrack.Start,  cuetrack.End, track.Duration);
                                track.Start = times.start;
                                track.End = times.end;
                                sheetTracks.Add(track);
                            }
                            else return;
                        }
                        


                        if (createdAlbum)
                        {
                            
                            album.Added = TimeUtils.Latest(sheetTracks.Select(x=>x.DateAdded));
                            album.Modified = TimeUtils.Latest(sheetTracks.Select(x=>x.Modified));
                            album.Created = TimeUtils.Earliest(sheetTracks.Select(x=>x.Created));
                            album.LastPlayed = null;
                            albums.Add((album.Title, album.AlbumArtist.Name), album);
                            try
                            {
                                album.DbInsert(Database);
                                Debug.Assert(album.DatabaseIndex > 0);
                            }
                            catch (Exception ex){Console.WriteLine(ex);}
                        }
                        
                        foreach (var t in sheetTracks)
                        {
                            try
                            {
                                t.DbInsert(Database);
                                Debug.Assert(t.DatabaseIndex > 0);
                            }
                            catch (Exception ex){Console.WriteLine(ex);}

                            foreach (var artist in t.Artists)
                            {
                                var sql = @"INSERT OR IGNORE INTO TrackArtists (TrackId, ArtistId) VALUES ($trackid, $artistid);
                                        SELECT last_insert_rowid();";
                                try
                                {
                                    Database.ExecuteScalar(sql, new()
                                    {
                                        ["$trackid"] = t.DatabaseIndex,
                                        ["$artistid"] = artist.DatabaseIndex,
                                    });    
                                }
                                catch (Exception ex){Console.WriteLine($"{ex} || sql query : {sql}");}

                            }
                        }

                        foreach (var track in sheetMultiTracks)
                        {
                            try
                            {
                                track.DbDelete(Database);
                            }
                            catch (Exception ex){Console.WriteLine(ex);}
                        }
                    });
                    // End Cue Sheets Handler
                    if (gotsheetback || list.Count == 0)
                        continue;
                    try
                    {
                        playlist.DatabaseInsert(Database);
                        Debug.Assert(playlist.DatabaseIndex > 0);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        continue;
                    }
                    
                    
                    const string insertTrackSql =
                        @"INSERT OR IGNORE INTO PlaylistTracks (PlaylistId, TrackId, Position) VALUES ($playlistid, $trackid, $position);
                          SELECT last_insert_rowid();";

                    int position = 0;
                    foreach (string trackPath in list)
                    {
                        if (tracks.TryGetValue(trackPath, out var track))
                        {
                            try
                            {
                                Database.ExecuteScalar(insertTrackSql, new()
                                {
                                    ["$playlistid"] = playlist.DatabaseIndex,
                                    ["$trackid"] = track.DatabaseIndex,
                                    ["$position"] = position
                                });
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(
                                    $"{ex} || playlistId : {playlist.DatabaseIndex}, trackid : {track.DatabaseIndex}, position : {position}");
                            }
                            finally
                            {
                                position++;
                            }
                        }
                        
                    }
                }
                Database.Commit();
            }
            catch
            {
                Database.Rollback();
                throw;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            Database.Close();
            progress.Progress.Report(("", 0, true));
        }
        
    }

    
    
    #endregion

}