using System;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace MusicaLibre.Services;

public static class ImageLoader
{
    public const int ThumbMaxSize = 200;
    public const int ImageMaxSize = 4000;

    public static (Bitmap?, PixelSize?)? LoadImage(string imagePath, int maxSize, CancellationToken token)
    {
        try
        {
            using var stream = File.OpenRead(imagePath);
            token.ThrowIfCancellationRequested();
            return DecodeImage(stream, maxSize, token);
        }
        catch (OperationCanceledException){}
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }
    
    public static (Bitmap?, PixelSize?)? DecodeImage(Stream stream, int maxSize, CancellationToken token)
    {
        PixelSize? pixelSize = null;
        try
        {
            token.ThrowIfCancellationRequested();
            
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);
            
            //don't let Create dispose of my stream (probably a rare bug but it happens)
            using var codec = SKCodec.Create(new NonDisposableStream(stream));
            if (codec != null)
            {
                var info = codec.Info;
                pixelSize = new PixelSize(info.Width, info.Height);
            }

            EnsureSeekable(ref stream);
            token.ThrowIfCancellationRequested();
            return (Bitmap.DecodeToWidth(stream, maxSize), pixelSize); // Decode directly from stream
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
        }

        return null;
    }

    
    private static void EnsureSeekable(ref Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
            return;
        }

        var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;

        stream.Dispose(); // Dispose the original stream if it's unseekable
        stream = memory;
    }
    public static (int width, int height) GetTargetDimensions(int imageWidth, int imageHeight, int maxSize)
    {
        float scale =  Math.Min(1f, maxSize / (float)Math.Max(imageWidth, imageHeight));
        int targetWidth = (int)(imageWidth * scale);
        int targetHeight = (int)(imageHeight * scale);
        return (targetWidth, targetHeight);
    }

    


    public static void WriteStreamToFile(Stream stream, string filePath)
    {
        
        using var fileStream = File.Create(filePath);
        stream.CopyTo(fileStream);
    }

    /*
    public static Bitmap? DecodeImage(Stream stream, int maxSize)
    {
        try
        {
            stream.Seek(0, SeekOrigin.Begin);
            using var skbmp = SKBitmap.Decode(stream);
            if (skbmp == null) return null;
            int imgW = skbmp.Info.Width;
            int imgH = skbmp.Info.Height;

            float scale =  Math.Min(1f, maxSize / (float)Math.Max(imgW, imgH));
            int targetWidth = Math.Max(1, (int)(imgW * scale));
            int targetHeight = Math.Max(1, (int)(imgH * scale));

            if (scale < 1f)
            {
                var resized = skbmp.Resize(new SKImageInfo(targetWidth, targetHeight), SKSamplingOptions.Default);
                if (resized == null) return null;
                return ConvertSkiaBitmapToAvalonia(resized);
            }
        }
        catch(Exception e){Console.WriteLine(e);}

        return null;
    }

*/
  


  
}

