using Avalonia.Input.TextInput;

namespace MusicaLibre.ViewModels;
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
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

public partial class LibraryViewModel:ViewModelBase
{
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
                    //Console.WriteLine($"Adding audio file : {audioFile.FullName}");
                    tracks.Add(audioFile.FullName, new Track()
                    {
                        FilePath = audioFile.FullName,
                        FileName = audioFile.Name,
                        FolderPathstr = audioFile.DirectoryName,
                        FileExtension = audioFile.Extension,
                        Modified = audioFile.LastWriteTime,
                        Created = audioFile.CreationTime,
                        LastPlayed = audioFile.LastAccessTime,
                        DateAdded = DateTime.Now,
                    });
                },
                (imageFile) =>
                {
                    //Console.WriteLine($"Adding image file : {imageFile.FullName}");
                    var artwork = new Artwork(Database)
                    {
                        SourcePath = imageFile.FullName,
                        FolderPathstr = imageFile.DirectoryName,
                        SourceType = ArtworkSourceType.External,
                        MimeType = PathUtils.GetMimeType(imageFile.Extension),
                        Role = null,
                        BookletPage = null
                    };
                    
                    artworkFiles.Add(artwork);
                },
                (playlistFile) =>
                {
                    //Console.WriteLine($"Adding playlist file : {playlistFile.FullName}");
                    var playlist = new Playlist()
                    {
                        FilePath = playlistFile.FullName,
                        FileName = playlistFile.Name,
                        FolderPathstr = playlistFile.DirectoryName,
                        Modified = playlistFile.LastWriteTime,
                        Created = playlistFile.CreationTime,
                    };
                    playlists.Add(playlist.FilePath, playlist);
                },
                (folderpath) =>
                {
                    folders.Add(folderpath, new Folder(folderpath));
                });

            Console.WriteLine("Processing Library data");
            Database.Open();
            Database.BeginTransaction();
            try
            {
                foreach (var folder in folders.Values)
                {
                    try
                    {
                        folder.DatabaseInsert(Database);
                        Debug.Assert(folder.DatabaseIndex != null);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
                Database.Commit();
            }
            catch (Exception ex) { Database.Rollback(); }
            
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
                    
                    if (artwork.Hash == null) continue;
                    if (!artworks.ContainsKey(artwork.Hash))
                    {
                        FindArtworkRole(artwork);
                        try
                        {
                            artwork.DataBaseInsert(Database);
                            if (artwork.DatabaseIndex != null)
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
                        var discNumber = file.Tag.Disc;
                        track.Comment = file.Tag.Comment;

                        track.Duration = file.Properties.Duration;
                        track.BitrateKbps = file.Properties.AudioBitrate;
                        track.SampleRate = file.Properties.AudioSampleRate;
                        track.Channels = file.Properties.AudioChannels;
                        try
                        {
                            var codecs = file.Properties.Codecs.Select(c => c.Description);
                            track.Codec = string.Join(", ", codecs);
                        }
                        catch (Exception ex)
                        {
                            //Console.WriteLine(ex);
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
                                Debug.Assert(year.DatabaseIndex != null);
                            }
                            catch (Exception ex){Console.WriteLine(ex);}
                        }
                        track.Year = year;
                        
                        var format = AudioFormat.Generate(track.Codec, track.BitrateKbps.Value);
                        if (!audioFormats.TryGetValue(format, out var audioFormat))
                        {
                            audioFormat = new AudioFormat(format);
                            audioFormats.Add(format, audioFormat);
                            try
                            {
                                audioFormat.DbInsert(Database);
                                Debug.Assert(audioFormat.DatabaseIndex != null);
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
                        
                        var albumArtists = file.Tag.AlbumArtists;
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
                        
                        Artist? albumArtist = null;
                        foreach (var str in performers)
                        {
                            var performer = str.Trim();
                            if (string.IsNullOrWhiteSpace(performer)) continue;
                            if(string.IsNullOrWhiteSpace(albumPerformer))
                                albumPerformer = performer;
                            if (!artists.TryGetValue(performer, out var artist))
                            {
                                try
                                {
                                    artist = new Artist(performer);
                                    artist.DatabaseInsert(Database);
                                    Debug.Assert(artist.DatabaseIndex != null, "artist.DatabaseIndex != null");
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
                            if (albumArtist == null)
                            {
                                if (artists.TryGetValue(albumPerformer, out var artist))
                                {
                                    albumArtist = artist;
                                }
                                else
                                {
                                    //Console.Error.WriteLine($"Null Album Artist vs Album mismatch in {track.FilePath} || albumPerformer :{albumPerformer} || null? :{(albumPerformer==null).ToString()}");
                                    //continue;    
                                }
                            }
                            albums.TryGetValue((file.Tag.Album, albumPerformer), out var album);
                            if (album == null)
                            {
                                album = new Album(file.Tag.Album, albumArtist, track.Year);
                                album.Folder = track.Folder;
                                albums.Add((file.Tag.Album, albumPerformer), album);
                                try
                                {
                                    album.DatabaseInsert(Database);
                                    Debug.Assert(album.DatabaseIndex != null, "album.DatabaseIndex != null");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }
                            }
                            
                            
                            track.Album = album;    
                            if(!discs.TryGetValue((album, discNumber), out var disc))
                            {
                                disc = new Disc(discNumber, album);
                                discs.Add((album, discNumber), disc);
                                try
                                {
                                    disc.DatabaseInsert(Database);
                                    Debug.Assert(disc.DatabaseIndex != null, "albumDisc.DatabaseIndex != null");
                                }
                                catch (Exception ex){Console.WriteLine(ex);}
                            }
                            track.Disc = disc;

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
                                if (artwork.Hash == null)
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
                                        artwork.DataBaseInsert(Database);
                                        Debug.Assert(artwork.DatabaseIndex != null, "artwork.DatabaseIndex != null");
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

                                if(track.Album != null && artwork.DatabaseIndex != null)
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
                                    genre.DatabaseInsert(Database);
                                    Debug.Assert(genre.DatabaseIndex != null, "genre.DatabaseIndex != null");
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
                                    publisher.DatabaseInsert(Database);
                                    Debug.Assert(publisher.DatabaseIndex != null, "publisher.DatabaseIndex != null");
                                    publishers.Add(file.Tag.Publisher, publisher);
                                }
                                catch (Exception ex){ Console.WriteLine(ex); }
                            }
                            if(publisher != null)
                                track.Publisher = publisher;
                        }


                        try
                        {
                            
                            track.DatabaseInsert(Database);
                            Debug.Assert(track.DatabaseIndex != null, "track.DatabaseIndex != null");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                            continue;
                        }
                        
                        foreach (var genre in track.Genres)
                        {
                            if (genre.DatabaseIndex == null)
                            {
                                Console.WriteLine($"Null genre index in {track.FilePath}");
                                continue;
                            }
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
                            if (artist.DatabaseIndex == null)
                            {
                                Console.WriteLine($"null artist database index in {track.FilePath}");
                                continue;
                            }
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
                            if (composer.DatabaseIndex == null)
                            {
                                Console.WriteLine($"null composer database index in {track.FilePath}");
                                continue;
                            }
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
                            if (artwork.DatabaseIndex == null)
                            {
                                Console.WriteLine($"Null artwork index in {track.FilePath}");
                                continue;
                            }
                            
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
                        albumfolders.Add(track.FolderPathstr!);
                        if(modified < track.Modified) modified = track.Modified.Value;
                        if(created >  track.Created) created = track.Created.Value;
                        if(album.AlbumArtist  == null)
                            foreach(var artist in track.Artists)
                                albumartists.Add(artist.Name);
                    }
                    
                    if(album.AlbumArtist == null && albumartists.Count > 0)
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
                    album.Modified = modified ;
                    album.Created = created;   

                    var rootFolder = (albumfolders.Count > 1) ? PathUtils.GetCommonRoot(albumfolders) : albumfolders.FirstOrDefault();

                    Folder rootf=null;
                    try
                    {
                        rootf = folders[rootFolder];
                    }
                    catch(Exception ex) {Console.WriteLine(ex.ToString());}

                    if (rootf != null)
                        album.Folder = rootf; 

                    var albumArtworks = artworks.Values.Where(x =>
                        x.FolderPathstr!.StartsWith(rootFolder!, StringComparison.Ordinal)).ToList();
                    if (albumArtworks.Count < 100)
                    {
                        foreach (var artwork in albumArtworks)
                            album.AddArtwork(artwork);
                    }
                    else
                    {
                        Console.WriteLine($"Too many artworks for album {album.Title}");
                    }
                    FindAlbumCover(album);

                    try
                    {
                        album.DatabaseUpdate(Database);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine( $"Failed to update album in database {album.Title} : {ex}");
                    }
                    
                    foreach (var artwork in album.Artworks)
                    {
                        var artworkId = artwork.DatabaseIndex;
                        if (artworkId == null) continue;
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
                    var list = Playlist.Load(playlist.FilePath!);
                    if (list.Count == 0)
                        continue;
                    try
                    {
                        playlist.DatabaseInsert(Database);
                        Debug.Assert(playlist.DatabaseIndex != null);
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
                            if (track.DatabaseIndex == null) 
                                continue;
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

    public void FindArtworkRole(Artwork artwork)
    {
        var filename = System.IO.Path.GetFileName(artwork.SourcePath);
        if (filename.Contains("cover", StringComparison.OrdinalIgnoreCase))
        {
            artwork.Role = ArtworkRole.CoverFront;
            return;
        }
        if (filename.Contains("front", StringComparison.OrdinalIgnoreCase))
        {
            artwork.Role = ArtworkRole.CoverFront;
            return;
        }
        if (filename.Contains("folder", StringComparison.OrdinalIgnoreCase))
        {
            artwork.Role = ArtworkRole.CoverFront;
            return;
        }
        if (filename.Contains("back", StringComparison.OrdinalIgnoreCase))
        {
            artwork.Role = ArtworkRole.CoverBack;
            return;
        }
        if (filename.Contains("disc", StringComparison.OrdinalIgnoreCase))
        {
            artwork.Role = ArtworkRole.Disk;
            return;
        }
        if (filename.Contains("cd", StringComparison.OrdinalIgnoreCase))
        {
            artwork.Role = ArtworkRole.Disk;
            return;
        }
        if (filename.Contains("vinyl", StringComparison.OrdinalIgnoreCase))
        {
            artwork.Role = ArtworkRole.Disk;
            return;
        }
        if (filename.Contains("artist", StringComparison.OrdinalIgnoreCase))
        {
            artwork.Role = ArtworkRole.Artist;
            return;
        }
        if (filename.Contains("booklet", StringComparison.OrdinalIgnoreCase))
        {
            artwork.Role = ArtworkRole.Booklet;
            return;
        }
        if (filename.Contains("inlay", StringComparison.OrdinalIgnoreCase))
        {
            artwork.Role = ArtworkRole.Inlay;
            return;
        }
        if (filename.Contains("tray", StringComparison.OrdinalIgnoreCase))
        {
            artwork.Role = ArtworkRole.Inlay;
            return;
        }
        artwork.Role = ArtworkRole.Other;
    }
    public void FindAlbumCover(Album album)
    {
        if (album.Artworks.Count == 0) return;
        else if (album.Artworks.Count == 1)
        {
            album.Cover = album.Artworks.First();
            return;
        }
        foreach (var artwork in album.Artworks)
        {
            if (artwork.Role == ArtworkRole.CoverFront )
            {
                album.Cover = artwork;
                return;
            }

            if (artwork.Role == ArtworkRole.Other)
            {
                var filename = System.IO.Path.GetFileNameWithoutExtension(artwork.SourcePath);
                var escapedTitle = Regex.Escape(album.Title);
                var pattern = Regex.Replace(escapedTitle, @"\s+", @"\W+"); 
                bool match = Regex.IsMatch(filename, pattern, RegexOptions.IgnoreCase);

                if ( match)
                {
                    album.Cover = artwork;
                    return;
                }
            }
            
        }

        foreach (var artwork in album.Artworks)
        {

        }
        album.Cover = album.Artworks.First();
    }
    #endregion

}