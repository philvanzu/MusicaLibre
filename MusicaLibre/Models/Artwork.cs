using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageMagick;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;
using MusicaLibre.Views;
using SkiaSharp;

namespace MusicaLibre.Models;

public class Artwork:IDisposable
{
    public long DatabaseIndex { get; set; }
    public string Hash { get; set; }
    public ArtworkSourceType? SourceType { get; set; }
    public ArtworkRole? Role { get; set; }
    public string SourcePath { get; set; } // File path, UNC path, or URL
    public string FolderPathstr { get; set; }
    public Folder Folder { get; set; }
    public long FolderId { get; set; }
    public int? BookletPage { get; set; }
    public byte[]? ThumbnailData { get; set; } // only stores a value before db insertion 
    public int Width { get; set; }
    public int Height { get; set; }
    public string MimeType { get; set; } // e.g., "image/jpeg", "image/png"
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
        if(DatabaseIndex == 0 || string.IsNullOrEmpty(Hash)) return;
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

    const string insertSql = @"
        INSERT INTO Artworks (Hash, Width, Height, Thumbnail, MimeType, SourcePath, FolderId, SourceType, Role, EmbedIdx, BookletPage)
        VALUES ($hash, $width, $height, $thumb, $mime, $sourcePath, $sourceFolder, $sourceType, $role, $embedIdx,  $bookletPage);
        SELECT last_insert_rowid();";
    const string selectSql = "SELECT * FROM Artworks;";
    const string deleteSql = "DELETE FROM Artworks WHERE Id=$id;";
    private Dictionary<string, object?> Parameters => new()
    {
        ["$id"] = DatabaseIndex,
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
    };
    
    public void DbInsert(Database db)
    {
        var id = db.ExecuteScalar(insertSql, Parameters );
        DatabaseIndex =  Convert.ToInt64(id);
    }

    public async Task DbInsertAsync(Database db, Action<long>? callback=null)
    {
        var id = await db.ExecuteScalarAsync(insertSql, Parameters );
        DatabaseIndex =  Convert.ToInt64(id);
        callback?.Invoke(DatabaseIndex);
    }
    public async Task DbDeleteAsync(Database db)
        => await db.ExecuteNonQueryAsync(deleteSql, Parameters );
    public static Dictionary<long, Artwork> FromDatabase(Database db)
        => ProcessReaderResult(db.ExecuteReader(selectSql), db);
    
    public static async Task<Dictionary<long, Artwork>> FromDatabaseAsync(Database db)
        => ProcessReaderResult(await db.ExecuteReaderAsync(selectSql), db);

    static Dictionary<long, Artwork> ProcessReaderResult(List<Dictionary<string, object?>> result, Database db)
    {
        Dictionary<long,Artwork> artworks = new();
        foreach (var row in result)
        {
            Artwork artwork = new Artwork(db)
            {
                DatabaseIndex     = Convert.ToInt64(row["Id"]),
                Hash              = (string)row["Hash"],
                Width             = Convert.ToInt32(row["Width"]),
                Height            = Convert.ToInt32(row["Height"]),
                //Thumbnail         = row["Thumbnail"] as byte[],
                MimeType          = (string)row["MimeType"],
                SourcePath        = (string)row["SourcePath"],
                FolderId      = Convert.ToInt64(row["FolderId"]),
                SourceType        = Database.GetEnum<ArtworkSourceType>(row, "SourceType"),
                Role              = Database.GetEnum<ArtworkRole>(row, "Role"),
                EmbedIdx     = Database.GetValue<int>(row, "EmbedIdx"),
                BookletPage = Database.GetValue<int>(row, "BookletPage")
            };

            artworks.Add(artwork.DatabaseIndex, artwork);
        }

        return artworks;
    }

    public static async Task <Artwork?>InsertIfNotExist(
        LibraryViewModel library, 
        string imagePath, 
        ArtworkRole role, 
        ArtworkSourceType sourceType,
        Folder? defaultFolder = null)
    {
        var srcFolderPath = Path.GetDirectoryName(imagePath); 
        var folder = library.Data.Folders.Values.FirstOrDefault(f => f.Name.Equals(srcFolderPath));
        if (folder is null)
        {
            if (defaultFolder is null)
            {
                var importPath = Path.Combine(library.Path, AppData.Instance.UserSettings.ImagesImportPath, Path.GetFileName(imagePath));
                folder = library.Data.Folders.Values.FirstOrDefault(x=>x.Name.Equals(importPath));
                if (folder is null)
                {
                    folder = new Folder(importPath); 
                    await folder.DbInsertAsync(library.Database);
                    library.Data.Folders.Add(folder.DatabaseIndex, folder);
                }
            }
            else folder = defaultFolder;
            var dstPath = Path.Combine(folder.Name, Path.GetFileName(imagePath));
            File.Move(imagePath, dstPath);
            imagePath = dstPath;
        }
        var artwork = library.Data.Artworks.Values.FirstOrDefault(x => x.SourcePath.Equals(imagePath));
        if (artwork == null)
        {
            artwork = new Artwork(library.Database)
            {
                SourcePath = imagePath,
                FolderPathstr = folder.Name,
                Folder = folder,
                SourceType = sourceType,
                MimeType = PathUtils.GetMimeType(Path.GetExtension(imagePath)) ?? "image/jpeg",
                Role = role,
            };
            using var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            artwork.ProcessImage(fileStream);
            if (string.IsNullOrEmpty(artwork.Hash))
            {
                Console.WriteLine($"Failed to process picture");
                return null;
            }

            var existing = library.Data.Artworks.Values.FirstOrDefault(x => x.Hash.Equals(artwork.Hash));
            if (existing != null) artwork = existing;
            else
            {
                try
                {
                    await artwork.DbInsertAsync(library.Database);
                    Debug.Assert(artwork.DatabaseIndex > 0);
                    library.Data.Artworks.Add(artwork.DatabaseIndex, artwork);
                }
                catch (Exception ex) { Console.WriteLine(ex); }
                finally { artwork.ThumbnailData = null; }
            }
        }

        return artwork;
    }

    public static async Task RemoveUninstanciatedEmbeddedArtworks(LibraryViewModel library)
    {
        var embeddedCounts = new Dictionary<long, int>();

        foreach (var track in library.Data.Tracks.Values)
        {
            foreach (var artwork in track.Artworks)
            {
                if (artwork.SourceType == ArtworkSourceType.Embedded)
                {
                    if (embeddedCounts.TryGetValue(artwork.DatabaseIndex, out var count))
                        embeddedCounts[artwork.DatabaseIndex] = count + 1;
                    else
                        embeddedCounts[artwork.DatabaseIndex] = 1;
                }
            }
        }

        var toRemove = library.Data.Artworks.Values
            .Where(x => x.SourceType == ArtworkSourceType.Embedded && !embeddedCounts.ContainsKey(x.DatabaseIndex))
            .ToList();

        foreach (var artwork in toRemove)
        {
            await artwork.DbDeleteAsync(library.Database);
            library.Data.Artworks.Remove(artwork.DatabaseIndex);
        }
    }

    public string? ProcessImage()
    {
        try
        {
            if (SourcePath == null) return "Null SourcePath";
            using var fileStream = new FileStream(SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ProcessImage(fileStream);
        }
        catch(Exception e){return e.ToString();}
    }
    public string? ProcessImage(Stream stream)
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
            return null;
        }
        catch (Exception e)
        {
            Width = 0;
            Height = 0;
            ThumbnailData = null;
            stream.Seek(0, SeekOrigin.Begin);
            return ProcessImageFallback(stream);
        }
    }

    public string? ProcessImageFallback(Stream stream)
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
            return null;
        }
        catch (Exception e)
        {
            Hash = string.Empty;
            Width = 0;
            Height = 0;
            ThumbnailData = null;
            return $"Failed to decode image in {SourcePath}";
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

    public void FindRole()
    {
        var filename = System.IO.Path.GetFileName(SourcePath);
        if (filename.Contains("cover", StringComparison.OrdinalIgnoreCase) 
            ||filename.Contains("front", StringComparison.OrdinalIgnoreCase)
            ||filename.Contains("folder", StringComparison.OrdinalIgnoreCase))
        {
            Role = ArtworkRole.CoverFront;
            return;
        }
        if (filename.Contains("back", StringComparison.OrdinalIgnoreCase))
        {
            Role = ArtworkRole.CoverBack;
            return;
        }
        if (filename.Contains("disc", StringComparison.OrdinalIgnoreCase)
            ||filename.Contains("cd", StringComparison.OrdinalIgnoreCase)
            ||filename.Contains("vinyl", StringComparison.OrdinalIgnoreCase))
        {
            Role = ArtworkRole.Disk;
            return;
        }
        if (filename.Contains("artist", StringComparison.OrdinalIgnoreCase))
        {
            Role = ArtworkRole.Artist;
            return;
        }
        if (filename.Contains("booklet", StringComparison.OrdinalIgnoreCase))
        {
            Role = ArtworkRole.Booklet;
            return;
        }
        if (filename.Contains("inlay", StringComparison.OrdinalIgnoreCase)
            ||filename.Contains("tray", StringComparison.OrdinalIgnoreCase))
        {
            Role = ArtworkRole.Inlay;
            return;
        }
        Role = ArtworkRole.Other;
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

