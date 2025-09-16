using System;
using System.Collections.Generic;
using MusicaLibre.Services;

namespace MusicaLibre.Models;

public class LibrarySnapshot
{
    public Dictionary<long, Track> Tracks { get; set; } = new();
    public Dictionary<long, Album> Albums { get; set; } = new();
    public Dictionary<(uint, long), Disc> Discs { get; set; } = new();
    public Dictionary<long, Artist> Artists { get; set; } = new();
    public Dictionary<long, Genre> Genres { get; set; } = new();
    public Dictionary<long, Publisher> Publishers { get; set; } = new();
    public Dictionary<long, AudioFormat> AudioFormats { get; set; } = new();
    public Dictionary<long, Artwork> Artworks { get; set; } = new();
    public Dictionary<long, Playlist> Playlists { get; set; } = new();
    public Dictionary<long, Year> Years { get; set; } = new();
    public Dictionary<long, Folder> Folders { get; set; } = new();
    
    public void Populate(Database db)
    {
        db.Open();
        try
        {
            Genres = Genre.FromDatabase(db);
            Publishers = Publisher.FromDatabase(db);
            Artists = Artist.FromDatabase(db);
            Tracks = Track.FromDatabase(db);
            AudioFormats = AudioFormat.FromDatabase(db);
            Artworks = Artwork.FromDatabase(db);
            Albums = Album.FromDatabase(db);
            Discs = Disc.FromDatabase(db);
            Playlists = Playlist.FromDatabase(db);
            Years = Year.FromDatabase(db);
            Folders = Folder.FromDatabase(db);

            //Resolve all foreign keys
            foreach (var track in Tracks.Values)
            {
                if(track.AlbumId != null) 
                    track.Album = Albums[track.AlbumId.Value];
                
                if(track.PublisherId != null) 
                    track.Publisher = Publishers[track.PublisherId.Value];
                
                if(track.ConductorId != null) 
                    track.Conductor = Artists[track.ConductorId.Value];
                
                if(track.RemixerId != null) 
                    track.Remixer = Artists[track.RemixerId.Value];
                
                track.AudioFormat = AudioFormats[track.AudioFormatId];
                track.Year = Years[track.YearId];
                track.Folder = Folders[track.FolderId];
            }
            foreach (var album in Albums.Values)
            {
                album.Folder = Folders[album.FolderId];
                album.AlbumArtist = Artists[album.ArtistId];
                album.Year = Years[album.YearId];
                if(album.CoverId != null) 
                    album.Cover = Artworks[album.CoverId.Value];
            }

            foreach (var disc in Discs.Values)
                if(disc.AlbumId > 0) 
                    disc.Album = Albums[disc.AlbumId];

            foreach (var artwork in Artworks.Values)
            {
                artwork.Folder = Folders[artwork.FolderId];
            }
            foreach (var playlist in Playlists.Values)
            {
                playlist.Folder = Folders[playlist.FolderId];
            }
            
            //Resolve all many to many relationships
            var sql = "Select * from TrackGenres";
            foreach (var row in db.ExecuteReader(sql))
            {
                var trackId = Database.GetValue<long>(row, "TrackId");
                var genreId = Database.GetValue<long>(row, "GenreId");
                Tracks[trackId!.Value].Genres.Add(Genres[genreId!.Value]);    
            }
            sql  = "Select * from TrackArtists";
            foreach (var row in db.ExecuteReader(sql))
            {
                var trackId = Database.GetValue<long>(row, "TrackId");
                var artistId = Database.GetValue<long>(row, "ArtistId");
                Tracks[trackId!.Value].Artists.Add(Artists[artistId!.Value]);    
            }
            sql  = "Select * from TrackComposers";
            foreach (var row in db.ExecuteReader(sql))
            {
                var trackId = Database.GetValue<long>(row, "TrackId");
                var artistId = Database.GetValue<long>(row, "ArtistId");
                Tracks[trackId!.Value].Composers.Add(Artists[artistId!.Value]);    
            }
            sql  = "Select * from TrackArtworks";
            foreach (var row in db.ExecuteReader(sql))
            {
                var trackId = Database.GetValue<long>(row, "TrackId");
                var artworkId = Database.GetValue<long>(row, "ArtworkId");
                Tracks[trackId!.Value].Artworks.Add(Artworks[artworkId!.Value]);    
            }
            sql =  "Select * from AlbumArtworks";
            foreach (var row in db.ExecuteReader(sql))
            {
                var albumId = Database.GetValue<long>(row, "AlbumId");
                var artworkId = Database.GetValue<long>(row, "ArtworkId");
                Albums[albumId!.Value].Artworks.Add(Artworks[artworkId!.Value]);    
            }
            sql = "Select * from PlaylistTracks";
            foreach (var row in db.ExecuteReader(sql))
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
            db.Close();
        }
    }
}