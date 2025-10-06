using System;
using System.Collections.Generic;
using System.Linq;
using MusicaLibre.Models;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Services;

public static class SearchUtils
{
    public static double GetScore(string[] searchTokens, string[] fieldTokens, double weight)
    {
        double score = 0.0;
        var tokens = fieldTokens.ToList(); // mutable copy
        double penalty = weight * 0.05;   // 5% of the field weight

        foreach (var search in searchTokens)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                if (string.Equals(tokens[i], search, StringComparison.OrdinalIgnoreCase))
                {
                    score += weight * 2;   // exact match bonus
                    tokens.RemoveAt(i);
                    break;
                }
                else if (tokens[i].Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    score += weight;       // partial match bonus
                    tokens.RemoveAt(i);
                    break;
                }
            }
        }

        // Penalize remaining unmatched tokens
        score -= tokens.Count * penalty;

        return score;
    }

    public static Dictionary<Track, double> FilterTracksAlt(string searchString, List<Track> tracks, LibraryViewModel library)
    {
        var splits = searchString.Split(' ').Select(x => x.Trim()).ToArray();
        
        var weights =  new Dictionary<Track, double>();
        var artistWeights = new Dictionary<Artist, double>();

        foreach (var artist in library.Data.Artists.Values)
        {
            string[] arr = { artist.Name };
            var aw = GetScore(splits, arr, 1.0);
            if (aw > 0)
                artistWeights[artist] = artistWeights.GetValueOrDefault(artist) + aw;
        }
        // Title matches – strongest
        foreach (var track in tracks)
        {
            
            var titleweight = string.IsNullOrWhiteSpace(track.Title)? 0 :
                GetScore(splits, track.Title.Split(' ').Select(x => x.Trim()).ToArray(), 1.0);

            var albumweight = track.Album is null ? 0 : 
                GetScore(splits, track.Album.Title.Split(' ').Select(x => x.Trim()).ToArray(), 1.0);

            double artistweight = 0;
            foreach(var kv in artistWeights)
                if(track.Artists.Contains(kv.Key))
                    artistweight += kv.Value;
                
            var weight = Math.Max(artistweight, Math.Max(titleweight, albumweight));
            if (weight > 0)
                weights[track] = weights.GetValueOrDefault(track) + weight;    
        }
        return weights;
    }

    public static Dictionary<Track, double> FilterTracks(string searchString, List<Track> tracks,
        LibraryViewModel library)
    {
        Dictionary<Track, double> weights = new Dictionary<Track, double>();
        foreach (var track in tracks)
        {
            double weight = 0;
            if (!string.IsNullOrEmpty(track.FilePath) && 
                track.FilePath.Contains(searchString, StringComparison.CurrentCultureIgnoreCase)) weight += 1;
            
            if(!string.IsNullOrEmpty(track.Title) && 
               track.Title.Contains(searchString, StringComparison.CurrentCultureIgnoreCase)) weight += 1;
            
            if(!string.IsNullOrEmpty(track.Album?.AlbumArtist?.Name) &&
                track.Album.AlbumArtist.Name.Contains(searchString, StringComparison.CurrentCultureIgnoreCase)) weight += 0.7;
            
            if(string.Join(" ", track.Artists.Select(x => x.Name))
               .Contains(searchString, StringComparison.CurrentCultureIgnoreCase)) weight += 0.8;
            
            if(!string.IsNullOrEmpty(track.Album?.Title) &&
                track.Album.Title.Contains(searchString, StringComparison.CurrentCultureIgnoreCase)) weight += 3;
            
            if(!string.IsNullOrEmpty(track.Year?.Name) &&
                track.Year.Name.Equals(searchString, StringComparison.CurrentCultureIgnoreCase)) weight += 0.5;
            
            if(!string.IsNullOrEmpty(track.Publisher?.Name) &&
                track.Publisher.Name.Contains(searchString, StringComparison.CurrentCultureIgnoreCase)) weight += 0.7;
            
            if (string.Join(" ", track.Genres.Select(x => x.Name))
                .Contains(searchString, StringComparison.CurrentCultureIgnoreCase)) weight += 0.2;
            
            if(weight > 0)
                weights[track] = weights.GetValueOrDefault(track) + weight;
        }
        return weights; 
    }
}