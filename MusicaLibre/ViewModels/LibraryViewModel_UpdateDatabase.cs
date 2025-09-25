using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.Views;

namespace MusicaLibre.ViewModels;

public partial class LibraryViewModel
{
   
   //When Database is in async mode
   public async Task SyncDatabase()
   {
      //gather all data
      var progress = MainWindowViewModel.StatusProgress;
      progress.CancellationTokenSource = new CancellationTokenSource();
      var token = progress.CancellationTokenSource.Token; 
      List<FileInfo> audioFiles = new List<FileInfo>();
      List<FileInfo> playlistFiles = new List<FileInfo>();
      List<string> directories = new List<string>();
      List<FileInfo> artworkFiles = new List<FileInfo>();
      using var throttler = new SemaphoreSlim(4);
      var parse = ParseLibraryRecursiveAsync(Path, progress, throttler, 
            (audioFile) => audioFiles.Add(audioFile),
           (imageFile) => artworkFiles.Add(imageFile), 
          (playlistFile) => playlistFiles.Add(playlistFile),
           (folderpath) =>directories.Add(folderpath));
      
      TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;

      var snapshot = new LibrarySnapshot();
      
      var takeSnapshot = snapshot.PopulateAsync(Database);
      await Task.WhenAll(new []{takeSnapshot, parse});
      
      var foldersByPath = new Dictionary<string, Folder>();
      foreach (var folder in snapshot.Folders.Values)
         foldersByPath.TryAdd(folder.Name, folder);
      
      var tracksByPath = new Dictionary<string, Track>();
      foreach (var track in snapshot.Tracks.Values)
         tracksByPath.TryAdd(track.FilePath, track);
      
      var artworksByPath = new HashSet<string>();
      var artworksByHash = new Dictionary<string, Artwork>();
      foreach (var artwork in snapshot.Artworks.Values)
      {
         artworksByPath.Add(artwork.SourcePath);
         artworksByHash.Add(artwork.Hash, artwork);
      }
      
      var yearsByNumber = new Dictionary<uint, Year>();
      foreach (var year in snapshot.Years.Values)
         yearsByNumber.Add(year.Number, year);
      
      var audioformatsByName = new Dictionary<string, AudioFormat>();
      foreach (var audioFormat in snapshot.AudioFormats.Values)
         audioformatsByName.Add(audioFormat.Name, audioFormat);

      var artistsByName = new Dictionary<string, Artist>();
      foreach (var artist in snapshot.Artists.Values)
         artistsByName.Add(artist.Name, artist);
      
      var newAlbums = new List<Album>();
      var albumsByName = new Dictionary<(string, string), Album>();
      foreach (var album in snapshot.Albums.Values)
         albumsByName.TryAdd((album.Title, album.AlbumArtist.Name), album);


      var discsByAlbum = new Dictionary<(Album, uint), Disc>();
      foreach (var disc in snapshot.Discs.Values)
         discsByAlbum.TryAdd((disc.Album, disc.Number), disc);
      
      var genresByName = new Dictionary<string, Genre>();
      foreach (var genre in snapshot.Genres.Values)
         genresByName.Add(genre.Name, genre);
      
      var publishersByName = new Dictionary<string, Publisher>();
      foreach (var publisher in snapshot.Publishers.Values)
         publishersByName.Add(publisher.Name, publisher);
      
      var playlistsByPath = new Dictionary<string, Playlist>();
      foreach (var playlist in snapshot.Playlists.Values)
         playlistsByPath.Add(playlist.FilePath, playlist);

      // start processing
      //add new folders
      List<Task> batch = new List<Task>();

      try
      {
         int total = directories.Count;
         progress.Counter = 0;
         foreach (var path in directories)
         {
            progress.Counter = progress.Counter + 1;
            progress.Progress.Report(($"Syncing folders : {progress.Counter}/{total}", (double)progress.Counter / total, false));
            if (!foldersByPath.TryGetValue(path, out var folder))
            {
               folder = new Folder() { Name = path };
               batch.Add(folder.DbInsertAsync(Database, (id)=> snapshot.Folders.Add(id, folder)));
               foldersByPath.Add(path, folder);
            }
         }
         await Task.WhenAll(batch);
         batch.Clear();
      }
      catch (Exception ex){Console.WriteLine(ex);}
   
      
      
      //add new images
      try
      {
         var total = directories.Count;
         progress.Counter = 0;
         foreach (var info in artworkFiles)
         {
            progress.Counter = progress.Counter + 1;
            progress.Progress.Report(($"Syncing images : {progress.Counter}/{total}", (double)progress.Counter / total, false));
            if (!artworksByPath.Contains(info.FullName) 
                && !snapshot.DiscardedFiles.ContainsKey(info.FullName))
            {
               var artwork = new Artwork(Database)
               {
                  SourcePath = info.FullName,
                  FolderPathstr = info.DirectoryName ?? Path,
                  SourceType = ArtworkSourceType.External,
                  MimeType = PathUtils.GetMimeType(info.Extension) ?? info.Extension,
                  Role = null,
                  BookletPage = null,
                  Folder = foldersByPath[info.DirectoryName ?? Path]
               };
               var error = artwork.ProcessImage();
               if (string.IsNullOrEmpty(artwork.Hash))
               {
                  if (!snapshot.DiscardedFiles.ContainsKey(artwork.SourcePath))
                  {
                     try
                     {
                        var discarded = new DiscardedFile(artwork.SourcePath, error);
                        snapshot.DiscardedFiles.Add(artwork.SourcePath, discarded);
                        _ = discarded.DbInsertAsync(Database);    
                     }
                     catch (Exception ex) {Console.WriteLine(ex);}
                  }
                  
                  
                  continue;
               }
            
                        
            
               if (!artworksByHash.ContainsKey(artwork.Hash))
               {
                  artwork.FindRole();
                  batch.Add(artwork.DbInsertAsync(Database, (id)=>
                  {
                     snapshot.Artworks.Add(id, artwork);
                     artwork.ThumbnailData = null;
                  }));
                  artworksByPath.Add(artwork.SourcePath);
                  artworksByHash.Add(artwork.Hash, artwork);   
               }
            }
         }
         await Task.WhenAll(batch);
         batch.Clear();
      }
      catch (Exception ex){Console.WriteLine(ex);}

      
      
      
      //add new tracks
      try
      {
         var total =  audioFiles.Count;
         progress.Counter = 0;
         foreach (var info in audioFiles)
         {
            progress.Counter = progress.Counter + 1;
            progress.Progress.Report(($"Syncing audio files : {progress.Counter}/{total}", (double)progress.Counter / total, false));

            if (!tracksByPath.ContainsKey(info.FullName)
                && !snapshot.DiscardedFiles.ContainsKey(info.FullName))
            {
               var track = new Track()
               {
                  FilePath = info.FullName,
                  FileName = info.Name,
                  FolderPathstr = info.DirectoryName ?? Path,
                  FileExtension = info.Extension,
                  Modified = info.LastWriteTime,
                  Created = info.CreationTime,
                  LastPlayed = info.LastAccessTime,
                  DateAdded = DateTime.Now
               };
               
               try
               {
                  track.Folder = foldersByPath[track.FolderPathstr];
                  track.FolderId = track.Folder.DatabaseIndex;

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
                  if (!yearsByNumber.TryGetValue(y, out var year))
                  {
                     year = new Year(y);
                     yearsByNumber.Add(y, year);
                     try
                     {
                        await year.DbInsertAsync(Database);
                        Debug.Assert(year.DatabaseIndex > 0);
                        snapshot.Years.Add(year.DatabaseIndex, year);
                     }
                     catch (Exception ex){Console.WriteLine(ex);}
                     
                  }
                  track.Year = year;
                  track.YearId = year.DatabaseIndex;

                  var format = AudioFormat.Generate(track.Codec, track.BitrateKbps);
                  if (!audioformatsByName.TryGetValue(format, out var audioFormat))
                  {
                     audioFormat = new AudioFormat(format);
                     audioformatsByName.Add(format, audioFormat);
                     try
                     {
                        await audioFormat.DbInsertAsync(Database);
                        Debug.Assert(audioFormat.DatabaseIndex > 0);
                        snapshot.AudioFormats.Add(audioFormat.DatabaseIndex, audioFormat);
                     }
                     catch (Exception ex){Console.WriteLine(ex);}
                     
                  }
                  track.AudioFormat = audioFormat;
                  track.AudioFormatId = audioFormat.DatabaseIndex;

                  var performers = file.Tag.Performers?? [];

                  var composers = file.Tag.Composers?? [];
                  performers = performers.Concat(composers).ToArray();

                  var albumPerformer = string.Join(" & ", file.Tag.AlbumArtists).Trim();
                  performers = performers.Concat([albumPerformer]).ToArray();

                  var conductor = file.Tag.Conductor;
                  if (!string.IsNullOrWhiteSpace(conductor))
                  {
                     conductor = conductor.Trim();
                     performers = performers.Concat([conductor]).ToArray();
                  }

                  var remixer = file.Tag.RemixedBy;
                  if (!string.IsNullOrWhiteSpace(remixer))
                  {
                     remixer = remixer.Trim();
                     performers = performers.Concat([remixer]).ToArray();
                  }

                  artistsByName.TryGetValue(_unknownArtistStr, out var unknownArtist);
                  Artist albumArtist = unknownArtist!;
                  
                  foreach (var str in performers.Distinct())
                  {
                     var performer = str.Trim();
                     if (string.IsNullOrWhiteSpace(performer))
                        continue;
                     if (!artistsByName.TryGetValue(performer, out var artist))
                     {
                        artist = new Artist(performer);
                        try
                        {
                           await artist.DbInsertAsync(Database);
                           Debug.Assert(artist.DatabaseIndex > 0);
                           artistsByName.Add(performer, artist);
                           snapshot.Artists.Add(artist.DatabaseIndex, artist);   
                        }
                        catch (Exception ex){Console.WriteLine(ex);}
                     }

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
                  
                  if(track.Artists.Count == 0)
                     track.Artists.Add(unknownArtist!);

                  var albumName = file.Tag.Album ?? 
                                  System.IO.Path.GetFileName(track.FolderPathstr);
                  albumName = albumName.Trim();

                  {
                     
                     if (albumArtist == unknownArtist 
                        && artistsByName.TryGetValue(albumPerformer, out var artist))
                        albumArtist = artist;



                     albumsByName.TryGetValue((albumName, albumArtist.Name), out var album);
                     if (album == null)
                     {
                        album = new Album(albumName, albumArtist, track.Year);
                        album.Folder = track.Folder;
                        album.FolderId = album.Folder.DatabaseIndex;
                        album.ArtistId = albumArtist.DatabaseIndex;
                        album.YearId = album.Year.DatabaseIndex;
                        
                        albumsByName.Add((albumName, albumArtist.Name), album);
                        try
                        {
                           await album.DbInsertAsync(Database);
                           Debug.Assert(album.DatabaseIndex > 0);
                           snapshot.Albums.Add(album.DatabaseIndex, album);
                           newAlbums.Add(album);   
                        }
                        catch (Exception ex){Console.WriteLine(ex);}
                        
                     }

                     track.Album = album; 
                     track.AlbumId = album.DatabaseIndex;
                     
                     if(!discsByAlbum.TryGetValue((album, track.DiscNumber), out var disc))
                     {
                        disc = new Disc(track.DiscNumber, album);
                        disc.AlbumId = album.DatabaseIndex;
                        discsByAlbum.Add((album, track.DiscNumber), disc);
                        try
                        {
                           await disc.DbInsertAsync(Database);
                           Debug.Assert(disc.DatabaseIndex > 0);
                           snapshot.Discs.Add(( disc.Number, album.DatabaseIndex), disc);
                        }
                        catch (Exception ex){Console.WriteLine(ex);}
                     }

                     int pictureIndex = 0;
                     foreach (var picture in file.Tag.Pictures)
                     {
                        var artwork = new Artwork(Database)
                        {
                           SourcePath = track.FilePath,
                           FolderPathstr = track.Folder.Name,
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

                        if (artworksByHash.TryGetValue(artwork.Hash, out var existing))
                           artwork =  existing;
                        else
                        {
                           try
                           {
                              await artwork.DbInsertAsync(Database);
                              Debug.Assert(artwork.DatabaseIndex > 0);
                              artworksByHash.Add(artwork.Hash, artwork);
                              snapshot.Artworks.Add(artwork.DatabaseIndex, artwork);
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

                     if (!genresByName.TryGetValue(gclean, out var genre))
                     {
                        try
                        {
                           genre = new Genre(gclean);
                           await genre.DbInsertAsync(Database);
                           Debug.Assert(genre.DatabaseIndex > 0);
                           genresByName.Add(gclean, genre);
                           snapshot.Genres.Add(genre.DatabaseIndex, genre);
                        }
                        catch (Exception ex){ Console.WriteLine(ex); }

                     }
                     if( genre != null)
                        track.Genres.Add(genre);
                  }

                  if (!string.IsNullOrEmpty(file.Tag.Publisher))
                  {
                     if (!publishersByName.TryGetValue(file.Tag.Publisher, out var publisher))
                     {
                        try
                        {
                           publisher = new Publisher(file.Tag.Publisher);
                           await publisher.DbInsertAsync(Database);
                           Debug.Assert(publisher.DatabaseIndex > 0);
                           publishersByName.Add(file.Tag.Publisher, publisher);
                           snapshot.Publishers.Add(publisher.DatabaseIndex, publisher);
                        }
                        catch (Exception ex){ Console.WriteLine(ex); }
                     }

                     if (publisher != null)
                     {
                        track.Publisher = publisher;
                        track.PublisherId = publisher.DatabaseIndex;
                     }
                  }


                  try
                  {
                     await track.DbInsertAsync(Database);
                     tracksByPath.Add(track.FilePath, track);
                     Debug.Assert(track.DatabaseIndex > 0);
                     snapshot.Tracks.Add(track.DatabaseIndex, track);
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
                        await Database.ExecuteScalarAsync(sql, new()
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
                        await Database.ExecuteScalarAsync(sql, new()
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
                        await Database.ExecuteScalarAsync(sql, new()
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
                        await Database.ExecuteScalarAsync(sql, new()
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
                  if (!snapshot.DiscardedFiles.ContainsKey(track.FilePath))
                  {
                     try
                     {
                        var discard = new DiscardedFile(track.FilePath,
                           $"Could not open {track.FilePath} with Taglib. {ex}");
                        _ = discard.DbInsertAsync(Database); 
                        snapshot.DiscardedFiles.Add(track.FilePath, discard);
                     }
                     catch (Exception exx) {Console.WriteLine(exx);}   
                  }
               }
            }            
         }
      }
      catch (Exception ex){Console.WriteLine(ex);}

      

         
      
      //process new Albums
      try
      {
          progress.Counter = 0;
          var total = newAlbums.Count;
          foreach (var album in newAlbums)
          {
              progress.Counter++;
              progress.Progress.Report(($"Processing Albums : {progress.Counter}/{total}",
                  (double)progress.Counter / total, false));

              HashSet<string> albumfolders = new HashSet<string>();
              var albumtracks = snapshot.Tracks.Values.Where(x => x.Album == album);

              DateTime modified = DateTime.MinValue;
              DateTime created = DateTime.MaxValue;
              foreach (var track in albumtracks)
              {
                  albumfolders.Add(track.FolderPathstr);
                  if (modified < track.Modified) modified = track.Modified;
                  if (created > track.Created) created = track.Created;

              }

              album.Added = DateTime.Now;
              album.Modified = modified;
              album.Created = created;

              
              var rootFolder = albumfolders.Count switch
              {
                  1 => null,
                  > 1 =>  PathUtils.GetCommonRoot(albumfolders),
                  _ => foldersByPath.First().Value.Name
              };

              Folder? rootf = null;
              if(!string.IsNullOrEmpty(rootFolder))
              {
                  try
                  {
                      rootf = foldersByPath[rootFolder];
                  }
                  catch (Exception ex) { Console.WriteLine(ex); }
              }
              
              if (rootf != null)
                  album.Folder = rootf;

              var albumArtworks = snapshot.Artworks.Values.Where(x =>
                 !string.IsNullOrWhiteSpace(x.FolderPathstr) &&
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
              if (album.Cover != null)
                 album.CoverId = album.Cover.DatabaseIndex;

              try
              {
                  await album.DbUpdateAsync(Database);
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
                      await Database.ExecuteNonQueryAsync(sql, new()
                      {
                          ["$albumid"] = album.DatabaseIndex,
                          ["$artworkid"] = artworkId,
                      });    
                  }
                  catch (Exception ex){Console.WriteLine($"{ex} || sql query : {sql}");}
              }
          }
      }
      catch (Exception e){Console.WriteLine(e);}
      
      //add new playlists
      try
      {
         var total = playlistFiles.Count;
         progress.Counter = 0;
         
         foreach (var info in playlistFiles)
         {
            progress.Counter = progress.Counter + 1;
            progress.Progress.Report(($"Syncing audio files : {progress.Counter}/{total}", (double)progress.Counter / total, false));
            
            if (!playlistsByPath.ContainsKey(info.FullName) && !snapshot.DiscardedFiles.ContainsKey(info.FullName))
            {
               var playlist = new Playlist()
               {
                  FilePath = info.FullName,
                  FileName = info.Name,
                  FolderPathstr = info.DirectoryName??Path,
                  Modified = info.LastWriteTime,
                  Created = info.CreationTime,
               };
               playlistsByPath.Add(playlist.FilePath, playlist);
               
               try
               {
                    playlist.Folder = foldersByPath[playlist.FolderPathstr];
                    playlist.FolderId = playlist.Folder.DatabaseIndex;
                    
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
                            if (!artistsByName.TryGetValue(performer, out var artist))
                            {
                                artist = new Artist(performer);
                                artistsByName.Add(performer, artist);
                                try
                                {
                                    artist.DbInsertAsync(Database).GetAwaiter().GetResult();
                                    Debug.Assert(artist.DatabaseIndex > 0);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }
                            }
                        }

                        bool createdAlbum = false;
                        if(! albumsByName.TryGetValue((sheet.Title, sheet.Performer), out var album))
                        {
                           var a = artistsByName[sheet.Performer];
                           var c = snapshot.Artworks.Values.FirstOrDefault(x => x.Folder == playlist.Folder);
                            album = new Album()
                            {
                                Folder = playlist.Folder,
                                FolderId = playlist.Folder.DatabaseIndex,
                                Title = sheet.Title,
                                AlbumArtist = a,
                                ArtistId = a.DatabaseIndex,
                                Cover = c,
                                CoverId = c?.DatabaseIndex,
                            };
                            createdAlbum = true;
                        }

                        if (!audioformatsByName.TryGetValue("Cue Pseudo Track", out var cueFormat))
                        {
                           cueFormat = new AudioFormat("Cue Pseudo Track");
                           cueFormat.DbInsertAsync(Database).GetAwaiter().GetResult();
                        }
                        
                        List<Track> sheetTracks = new List<Track>();
                        HashSet<Track> sheetMultiTracks = new HashSet<Track>();
                        foreach (var cuetrack in sheet.Tracks)
                        {
                            if (tracksByPath.TryGetValue(cuetrack.File, out var multitrack))
                            {
                                sheetMultiTracks.Add(multitrack);
                                var track = Track.Copy(multitrack);
                                if(createdAlbum && album.Year is null)
                                    album.Year = track.Year;    
                                track.Title = cuetrack.Title;
                                track.TrackNumber = cuetrack.Number;
                                track.Album = album;
                                track.AlbumId = album.DatabaseIndex;
                                track.AudioFormatId = cueFormat.DatabaseIndex;
                                track.AudioFormat =  cueFormat;
                                
                                if (!string.IsNullOrWhiteSpace(cuetrack.Performer) &&
                                    artistsByName.TryGetValue(cuetrack.Performer, out var artist))
                                {
                                    if(!track.Artists.Contains(artist))
                                        track.Artists.Add( artist);    
                                }
                                var start = cuetrack.Indexes.First().Start;
                                var end = cuetrack.Indexes.Last().End;
                                var times = TimeUtils.GetCueTrackTimes(start,  end, track.Duration);
                                track.Start = times.start;
                                track.End = times.end;
                                sheetTracks.Add(track);
                            }
                            else
                            {
                               if (!snapshot.DiscardedFiles.ContainsKey(playlist.FilePath))
                               {
                                  var discard = new DiscardedFile(playlist.FilePath, "Cue Sheet contains missing files");
                                  snapshot.DiscardedFiles.Add(playlist.FilePath, discard);
                                  try { discard.DbInsertAsync(Database).GetAwaiter().GetResult(); }
                                  catch (Exception ex) { Console.WriteLine(ex); }   
                               }
                               return;
                            }
                        }
                        


                        if (createdAlbum)
                        {
                            
                            album.Added = TimeUtils.Latest(sheetTracks.Select(x=>x.DateAdded));
                            album.Modified = TimeUtils.Latest(sheetTracks.Select(x=>x.Modified));
                            album.Created = TimeUtils.Earliest(sheetTracks.Select(x=>x.Created));
                            album.LastPlayed = null;
                            albumsByName.Add((album.Title, album.AlbumArtist.Name), album);
                            try
                            {
                                album.DbInsertAsync(Database).GetAwaiter().GetResult();
                                Debug.Assert(album.DatabaseIndex > 0);
                                snapshot.Albums.Add(album.DatabaseIndex, album);
                            }
                            catch (Exception ex){Console.WriteLine(ex);}
                        }
                        
                        foreach (var t in sheetTracks)
                        {
                            t.AlbumId = t.Album.DatabaseIndex;
                            try
                            {
                                t.DbInsertAsync(Database).GetAwaiter().GetResult();
                                Debug.Assert(t.DatabaseIndex > 0);
                                snapshot.Tracks.Add(t.DatabaseIndex, t);
                            }
                            catch (Exception ex){Console.WriteLine(ex);}

                            foreach (var artist in t.Artists)
                            {
                                var sql = @"INSERT OR IGNORE INTO TrackArtists (TrackId, ArtistId) VALUES ($trackid, $artistid);
                                        SELECT last_insert_rowid();";
                                try
                                {
                                    Database.ExecuteScalarAsync(sql, new()
                                    {
                                        ["$trackid"] = t.DatabaseIndex,
                                        ["$artistid"] = artist.DatabaseIndex,
                                    }).GetAwaiter().GetResult();    
                                }
                                catch (Exception ex){Console.WriteLine($"{ex} || sql query : {sql}");}

                            }
                        }

                        foreach (var track in sheetMultiTracks)
                        {
                            try
                            {
                                track.DbDeleteAsync(Database).GetAwaiter().GetResult();
                                snapshot.Tracks.Remove(track.DatabaseIndex);
                            }
                            catch (Exception ex){Console.WriteLine(ex);}
                        }
                    });
                    // End Cue Sheets Handler
                    if (gotsheetback || list.Count > 0)
                    {
                       try
                       {
                          await playlist.DatabaseInsertAsync(Database);
                          Debug.Assert(playlist.DatabaseIndex > 0);
                          snapshot.Playlists.Add(playlist.DatabaseIndex, playlist);
                       }
                       catch (Exception ex)
                       {
                          Console.WriteLine(ex);
                          continue;
                       }   
                    }
                    if (gotsheetback) continue;
                    
                    
                    const string insertTrackSql =
                        @"INSERT OR IGNORE INTO PlaylistTracks (PlaylistId, TrackId, Position) VALUES ($playlistid, $trackid, $position);
                          SELECT last_insert_rowid();";

                    int position = 0;
                    foreach (string trackPath in list)
                    {
                        if (tracksByPath.TryGetValue(trackPath, out var track))
                        {
                            try
                            {
                                await Database.ExecuteScalarAsync(insertTrackSql, new()
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
               catch(Exception ex) { Console.WriteLine(ex); }            
            }
         }
      }
      catch (Exception e){Console.WriteLine(e);}
      
      
      batch.Clear();
      //remove missing artwork
      var existingImages = new HashSet<string>();
      foreach(var info in artworkFiles)
         existingImages.Add(info.FullName);

      foreach (var artwork in snapshot.Artworks.Values.ToList())
      {
         if (artwork.SourceType == ArtworkSourceType.External && !existingImages.Contains(artwork.SourcePath))
         {
            batch.Add(artwork.DbDeleteAsync(Database));
            snapshot.Artworks.Remove(artwork.DatabaseIndex);
         }
      }
      
      //remove missing tracks
      var existingTracks = new HashSet<string>();
      foreach (var info in audioFiles)
         existingTracks.Add(info.FullName);
      foreach(var track in snapshot.Tracks.Values.ToList())
      {
         if (!existingTracks.Contains(track.FilePath))
         {
            batch.Add(track.DbDeleteAsync(Database));
            snapshot.Tracks.Remove(track.DatabaseIndex);
         }
      }
      
      //remove missing playlists
      var existingPlaylists = new HashSet<string>();
      foreach (var info in playlistFiles)
         existingPlaylists.Add(info.FullName);
      
      foreach(var playlist in playlistsByPath.Values)
         if (!existingPlaylists.Contains(playlist.FilePath))
            batch.Add(playlist.DbDeleteAsync(Database));
      
      await Task.WhenAll(batch);
      batch.Clear();
      
      //remove albums with zero tracks
      var trackCounts = snapshot.Tracks.Values
         .GroupBy(t => t.AlbumId)
         .ToDictionary(g => g.Key, g => g.Count());

      foreach(var album in snapshot.Albums.Values.ToList())
         if (!trackCounts.ContainsKey(album.DatabaseIndex))
         {
            batch.Add(album.DbDeleteAsync(Database));
            snapshot.Albums.Remove(album.DatabaseIndex);
         }
      
      await Task.WhenAll(batch);
      batch.Clear();
      
      // remove years with wero tracks
      trackCounts = snapshot.Tracks.Values
         .GroupBy(t => t.YearId)
         .ToDictionary(g => g.Key, g => g.Count());
      
      foreach(var year in snapshot.Years.Values.ToList())
         if (!trackCounts.ContainsKey(year.DatabaseIndex))
         {
            batch.Add(year.DbDeleteAsync(Database));
            snapshot.Years.Remove(year.DatabaseIndex);
         }
      
      // remove publishers with zero tracks
      trackCounts = snapshot.Tracks.Values
         .Where(x=>x.PublisherId.HasValue)
         .GroupBy(t => t.PublisherId!.Value)
         .ToDictionary(g => g.Key, g => g.Count());

      foreach(var publisher in snapshot.Publishers.Values.ToList())
         if (!trackCounts.ContainsKey(publisher.DatabaseIndex))
         {
            batch.Add(publisher.DbDeleteAsync(Database));
            snapshot.Publishers.Remove(publisher.DatabaseIndex);
         }
      
      // remove unused artists
      var referencedArtistIds = new HashSet<long>();

      // From tracks
      foreach (var t in snapshot.Tracks.Values)
      {
         foreach ( var a in t.Artists) referencedArtistIds.Add(a.DatabaseIndex);
         foreach (var a in t.Composers)  referencedArtistIds.Add(a.DatabaseIndex);
         if (t.RemixerId.HasValue)   referencedArtistIds.Add(t.RemixerId.Value);
         if (t.ConductorId.HasValue) referencedArtistIds.Add(t.ConductorId.Value);
      }
      // From albums
      foreach (var a in snapshot.Albums.Values)
         referencedArtistIds.Add(a.ArtistId);
      
      foreach (var a in snapshot.Artists.Values.ToList())
         if (!referencedArtistIds.Contains(a.DatabaseIndex))
         {
            batch.Add(a.DbDeleteAsync(Database));
            snapshot.Artists.Remove(a.DatabaseIndex);
         }

      await Task.WhenAll(batch);
      Dispatcher.UIThread.Post(()=>Data = snapshot);
      
      progress.Counter = 0;
      progress.Progress.Report(($"", -1, true));
      //progress.IsBusy = false;
      
      
   }
   
   
   
   
   
   
   
   
   
   
   
   
   
   
   
   public void DeleteGenre(Genre genre)
   {
      var tracks = Data.Tracks.Values.Where(x=>x.Genres.Contains(genre));
      foreach (var track in tracks)
      {
         track.Genres.Remove(genre);
         _ = track.UpdateGenresAsync(this);
         TagWriter.EnqueueFileUpdate(track);
      }
      
      Data.Genres.Remove(genre.DatabaseIndex);
      _ = genre.DbRemoveAsync(Database);
   }

   public Genre CreateGenre(string name, List<Track>? assignto = null)
   {
      var genre = new Genre(name);
      _ = genre.DbInsertAsync(Database);
      if(assignto != null)
         foreach (var track in assignto)
         {
            track.Genres.Add(genre);
            _ = track.UpdateGenresAsync(this);
            TagWriter.EnqueueFileUpdate(track);
         }
      return genre;
   }

   public void AddGenreToTracks(Genre genre, List<Track> tracks)
   {
      if (tracks == null || tracks.Count == 0) return;
      foreach (var track in tracks)
      {
         if (!track.Genres.Contains(genre))
         {
            track.Genres.Add(genre);
            _ = track.UpdateGenresAsync(this);
         }
      }
   }

   public void UpdateGenreArtwork(Genre genre, Artwork artwork)
   {
      genre.Artwork = artwork;
      _ = genre.DbUpdateAsync(Database);
   }

   public void AddDisc(uint number, Album album)
   {
      var disc = new Disc(number, album);
      Data.Discs.Add((number, album.DatabaseIndex), disc);
      _ = disc.DbInsertAsync(Database);
   }

   public bool IsAlbumEmpty(Album album)
   {
      return Data.Tracks.Values.Any(x=>x.Album == album);
   }

   public void RemoveEmptyAlbum(Album album)
   {
      foreach (var disc in Data.Discs.Values.Where(x => x.AlbumId == album.DatabaseIndex))
         _ = disc.DbDeleteAsync(Database);  
      _ = album.DbDeleteAsync(Database);
   }
   public void RemoveDisc(Disc disc)
   {
      _ = disc.DbDeleteAsync(Database);
      Data.Discs.Remove((disc.Number, disc.AlbumId));
   }

   public void AddFolder(Folder folder)
   {
      _ = folder.DbInsertAsync(Database);
      Data.Folders.Add(folder.DatabaseIndex, folder);
   }

   [RelayCommand]
   public void FixUnknownArtistAlbums()
   {

      var progress = new ProgressDialogViewModel();
      progress.CancellationTokenSource = new CancellationTokenSource();
      
      Dispatcher.UIThread.Post(() => _ = progress.Show(), DispatcherPriority.Render);
      
      _ = Task.Run(async () =>
      {
         try
         {
            await progress.DialogShown;
            await Task.Delay(1);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            
            // wait sync process to finish if running
            if (DbSyncManager.SyncTask != null) 
               await DbSyncManager.SyncTask;
        
            // set flag in DbSyncManager to delay sync process starting as long as dialog is open
            await DbSyncManager.SyncLock.WaitAsync();
            try
            {
               var fixables = Data.Albums.Values.Where(x => x.AlbumArtist.DatabaseIndex == 1).ToList();
               if (fixables.Count == 0) return;

               var tracksByAlbum = Data.Tracks.Values
                  .Where(x => x.Album != null)
                  .GroupBy(t => t.Album!)
                  .ToDictionary(g => g.Key, g => g.ToList());

               var albumsByArtist = Data.Albums.Values
                  .GroupBy(x => x.AlbumArtist)
                  .ToDictionary(g => g.Key, g => g.ToList());

               var total = fixables.Count;
               progress.Counter = 0;
               foreach (var unknownArtistAlbum in fixables)
               {
                  progress.Counter++;
                  progress.Progress.Report(($"Processing Albums : {progress.Counter}/{total}",
                     (double)progress.Counter / total, false));

                  if (tracksByAlbum.TryGetValue(unknownArtistAlbum, out var tracks))
                  {
                     var artists = tracks.SelectMany(x => x.Artists).Distinct().ToList();
                     if (artists.Count != 1) continue;
                     var artist = artists[0];

                     if (!albumsByArtist.TryGetValue(artist, out var albumCandidates))
                        albumCandidates = new List<Album>();

                     var albumTitle = unknownArtistAlbum!.Title;

                     List<Task> batch = new();
                     foreach (var track in tracks)
                     {
                        if (track.Album is null) continue;

                        var existing = albumCandidates?.FirstOrDefault(x => x.Title == albumTitle);
                        if (existing is null)
                        {
                           track.Album!.AlbumArtist = artist;
                           batch.Add(track.Album.DbUpdateAsync(Database));
                           albumCandidates!.Add(track.Album!);
                        }
                        else
                        {
                           track.Album = existing;
                           batch.Add(track.DbUpdateAsync(Database));
                        }

                        TagWriter.EnqueueFileUpdate(track);
                     }

                     await Task.WhenAll(batch);
                     batch.Clear();
                  }
               }
            }
            finally
            {
               progress.Progress.Report(($"Done",-1, true));
               DbSyncManager.SyncLock.Release();
            }
         }
         catch (Exception ex)
         {
            Console.WriteLine(ex);
         }
      });
   }

   public async Task<Album> CreateAlbum(string? title, List<Track> tracks)
   {
      await Task.CompletedTask;
      return null;
   } 
}