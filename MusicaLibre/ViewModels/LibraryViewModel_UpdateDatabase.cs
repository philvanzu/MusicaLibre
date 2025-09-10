using System.Collections.Generic;
using System.Linq;
using MusicaLibre.Models;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
   
   //When Database is in async mode

   
   public void DeleteGenre(Genre genre)
   {
      var tracks = Tracks.Values.Where(x=>x.Genres.Contains(genre));
      foreach (var track in tracks)
      {
         track.Genres.Remove(genre);
         _ = track.UpdateGenresAsync(this);
         TagUtils.EnqueueFileUpdate(track);
      }
      
      Genres.Remove(genre.DatabaseIndex!.Value);
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
            TagUtils.EnqueueFileUpdate(track);
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
      Discs.Add((number, album.DatabaseIndex.Value), disc);
      _ = disc.DbInsertAsync(Database);
   }

   public bool IsAlbumEmpty(Album album)
   {
      return Tracks.Values.Any(x=>x.Album == album);
   }

   public void RemoveEmptyAlbum(Album album)
   {
      foreach (var disc in Discs.Values.Where(x => x.AlbumId == album.DatabaseIndex))
         _ = disc.DbDeleteAsync(Database);  
      _ = album.DbDeleteAsync(Database);
   }
   public void RemoveDisc(Disc disc)
   {
      _ = disc.DbDeleteAsync(Database);
      Discs.Remove((disc.Number, disc.AlbumId));
   }

   public void AddFolder(Folder folder)
   {
      _ = folder.DbInsertAsync(Database);
      Folders.Add(folder.DatabaseIndex.Value, folder);
   }
}