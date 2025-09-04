using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageMagick;
using MusicaLibre.Services;
using SkiaSharp;

namespace MusicaLibre.Models;

public class Artwork:IDisposable
{
    public long? DatabaseIndex { get; set; }
    public string? Hash { get; set; }
    public ArtworkSourceType? SourceType { get; set; }
    public ArtworkRole? Role { get; set; }
    public string? SourcePath { get; set; } // File path, UNC path, or URL
    public string FolderPathstr { get; set; }
    public Folder? Folder { get; set; }
    public long? FolderId { get; set; }
    public int? BookletPage { get; set; }
    public byte[]? ThumbnailData { get; set; } // only stores a value before db insertion 
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? MimeType { get; set; } // e.g., "image/jpeg", "image/png"
    public int? EmbedIdx { get; set; }

    private Database _db;
    private ConcurrentDictionary<object, Action> _thumbnailWatchers = new();
    private Bitmap? _thumbnail;
    private Task? _thumbnailFetcherTask;
    private object _thumbnailFetcherLock = new object();
    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        set
        {
            _thumbnail = value;
            foreach( var notifyChangeRequest in _thumbnailWatchers.Values )
                notifyChangeRequest.Invoke();
        }
    }

    private int _thmbRefCount;

    public Artwork(Database db)
    {
        _db = db;
    }
    public void RequestThumbnail(object sender, Action notifyThumbnailChanged)
    {

        if (_thumbnailWatchers.TryAdd(sender, notifyThumbnailChanged))
            Interlocked.Increment(ref _thmbRefCount);

        if (Thumbnail != null)
        {
            notifyThumbnailChanged.Invoke();
            return;
        }
        lock (_thumbnailFetcherLock)
        {
            if (_thumbnailFetcherTask == null)
            {
                _thumbnailFetcherTask = Task.Run(async () =>
                {
                    try
                    {
                        var sql = $"Select Thumbnail From Artworks Where Id={DatabaseIndex};";
                        if (await _db.ExecuteScalarAsync(sql) is byte[] bytes)
                        {
                            try
                            {
                                using var stream = new MemoryStream(bytes);
                                var bmp = new Bitmap(stream);
                                Dispatcher.UIThread.Post(()=>
                                {
                                    if (_thmbRefCount > 0)
                                        Thumbnail = bmp;
                                    else
                                        bmp.Dispose();
                                });    
                            }
                            catch (Exception ex){Console.WriteLine(ex);}
                        }
                    }
                    catch (Exception ex){Console.WriteLine(ex);}
                }).ContinueWith((t) =>_thumbnailFetcherTask = null);    
            }    
        }
    }

    public void ReleaseThumbnail(object sender)
    {
        lock (_thumbnailFetcherLock)
        {
            if (_thumbnailWatchers.TryRemove(sender, out var action) && 
                Interlocked.Decrement(ref _thmbRefCount) == 0)
            {
                Thumbnail?.Dispose();
                Thumbnail = null;
            }    
        }
    }

    public void Dispose()
    {
        _thumbnailWatchers.Clear();
        // Atomically take ownership of the reference
        var old = Interlocked.Exchange(ref _thumbnail, null);
        old?.Dispose();

        _thmbRefCount = 0;
    }


    public override bool Equals(object? obj) => obj is Artwork a && Equals(a);
    public bool Equals(Artwork? other) => other is not null &&
                                          string.Equals(Hash, other.Hash, StringComparison.Ordinal);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Hash); // deterministic across runtimes

    public void DataBaseInsert(Database db)
    {
        const string sql = @"
        INSERT INTO Artworks (Hash, Width, Height, Thumbnail, MimeType, SourcePath, FolderId, SourceType, Role, EmbedIdx, BookletPage)
        VALUES ($hash, $width, $height, $thumb, $mime, $sourcePath, $sourceFolder, $sourceType, $role, $embedIdx,  $bookletPage);
        SELECT last_insert_rowid();";

        var id = db.ExecuteScalar(sql, new()
        {
            ["$hash"] = Hash,
            ["$width"] = Width,
            ["$height"] = Height,
            ["$thumb"] = ThumbnailData,
            ["$mime"] = (object?)MimeType ?? DBNull.Value,
            ["$sourcePath"] = (object?)SourcePath ?? DBNull.Value,
            ["$sourceFolder"] = Folder?.DatabaseIndex,
            ["$sourceType"] = SourceType,
            ["$role"] = Role,
            ["$embedIdx"] = EmbedIdx,
            ["$bookletPage"] = BookletPage,
        });

        DatabaseIndex =  Convert.ToInt64(id);
    }
    
    public static Dictionary<long, Artwork> FromDatabase(Database db, int[]? indexes=null)
    {
        string filter = String.Empty;
        if (indexes != null && indexes.Length == 0)
            filter = $"WHERE Id IN ({string.Join(", ", indexes)})";
    
        string sql = $@"
        SELECT * FROM Artworks {filter}";

        Dictionary<long,Artwork> artworks = new();
        foreach (var row in db.ExecuteReader(sql))
        {
            Artwork artwork = new Artwork(db)
            {
                DatabaseIndex     = (int)Database.GetValue<int>(row, "Id")!,
                Hash              = Database.GetString(row, "Hash"),
                Width             = Database.GetValue<int>(row, "Width"),
                Height            = Database.GetValue<int>(row, "Height"),
                //Thumbnail         = row["Thumbnail"] as byte[],
                MimeType          = Database.GetString(row, "MimeType"),
                SourcePath        = Database.GetString(row, "SourcePath"),
                FolderId      = Database.GetValue<long>(row, "FolderId"),
                SourceType        = Database.GetEnum<ArtworkSourceType>(row, "SourceType"),
                Role              = Database.GetEnum<ArtworkRole>(row, "Role"),
                EmbedIdx     = Database.GetValue<int>(row, "EmbedIdx"),
                BookletPage = Database.GetValue<int>(row, "BookletPage")
            };

            artworks.Add(artwork.DatabaseIndex.Value, artwork);
        }

        return artworks;
    }
    
    public void ProcessImage()
    {
        try
        {
            if (SourcePath == null) return;
            using var fileStream = new FileStream(SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ProcessImage(fileStream);
        }
        catch(Exception e){Console.WriteLine(e);}
    }
    public void ProcessImage(Stream stream)
    {
        try
        {
            stream.Seek(0, SeekOrigin.Begin);
            using var sha = SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(stream);
            Hash = Convert.ToHexString(hashBytes);
            
            stream.Seek(0, SeekOrigin.Begin);
            using var original = SKBitmap.Decode(new NonDisposableStream(stream));
            if (original == null)
                throw(new InvalidOperationException($"Artwork creation failed, stream could not be decoded for {SourcePath}"));
            Width = original.Width;
            Height = original.Height;
            
            if( Width <= 0 || Height <= 0)
                throw(new InvalidOperationException($"Artwork creation failed, invalid dimensions detected for {SourcePath}"));
            var resized = ResizeSkBitmap(original, 200);
            if (resized != null)
            {
                using var image = SKImage.FromBitmap(resized);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                ThumbnailData = data.ToArray();
            }
        }
        catch (Exception e)
        {
            Width = null;
            Height = null;
            ThumbnailData = null;
            stream.Seek(0, SeekOrigin.Begin);
            ProcessImageFallback(stream);
        }
    }

    public void ProcessImageFallback(Stream stream)
    {
        try
        {
            using var img = new MagickImage(stream);
            if (img == null)
                throw(new InvalidOperationException($"Artwork creation failed, stream could not be decoded for {SourcePath}"));
            Width = (int)img.Width;
            Height = (int)img.Height;
            if( Width <= 0 || Height <= 0)
                throw(new InvalidOperationException($"Artwork creation failed, invalid dimensions detected for {SourcePath}"));
            
            float scale = 200 / (float)Math.Max(img.Width, img.Height);
            uint targetWidth =  (uint)(img.Width * scale);
            uint targetHeight = (uint)(img.Height * scale);
            img.Resize(targetWidth, targetHeight);
            img.Format = MagickFormat.Jpeg;
            ThumbnailData = img.ToByteArray();
        }
        catch (Exception e)
        {
            Hash = null;
            Width = null;
            Height = null;
            ThumbnailData = null;
            Console.WriteLine($"Failed to decode image in {SourcePath}");
        }
    }

    public static SKBitmap? ResizeSkBitmap(SKBitmap sourceBitmap, int maxSize)
    {
        // Resize if necessary
        int imgW = sourceBitmap.Width;
        int imgH = sourceBitmap.Height;

        float scale = maxSize / (float)Math.Max(imgW, imgH);
        int targetWidth =  (int)(imgW * scale);
        int targetHeight = (int)(imgH * scale);

        return sourceBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKSamplingOptions.Default);
    }

    public void DbInsert(Database db)
    {
        
    }

}
public enum ArtworkSourceType
{
    Embedded,   // Inside the file tags
    External,   // External file on disk
    Remote,     // URL (e.g., cover from Last.fm)
    Generated,  // Auto-generated placeholder
}
public enum ArtworkRole
{
    CoverFront,
    CoverBack,
    Booklet,
    Inlay,
    Artist,
    Other,
    Disk
}

