using System;
using System.IO;

namespace MusicaLibre.Services;

public class FileSystemWatcherService : IDisposable
{
    private readonly FileSystemWatcher _watcher;

    public event Action<FileChangeEvent>? FileChanged;

    public FileSystemWatcherService(string path, Action<FileChangeEvent>? fileChanged )
    {
        FileChanged = fileChanged;
        _watcher = new FileSystemWatcher(path, "*.*")
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = false,
            NotifyFilter = NotifyFilters.FileName | 
                           NotifyFilters.DirectoryName | 
                           NotifyFilters.LastWrite
        };

        _watcher.Created += (_, e) => Raise(e);
        _watcher.Deleted += (_, e) => Raise(e);
        _watcher.Changed += (_, e) => Raise(e);
        _watcher.Renamed += (_, e) => Raise(e);
        _watcher.Error += (_, e) =>
        {
            // If buffer overflows, signal a "reset"
            FileChanged?.Invoke(new FileChangeEvent(path, WatcherChangeTypes.All, true));
        };
    }

    private void Raise(FileSystemEventArgs e)
    {
        // Handle directories explicitly
        if (Directory.Exists(e.FullPath) || e.ChangeType == WatcherChangeTypes.Deleted)
        {
            var ev = new FileChangeEvent(e.FullPath, e.ChangeType, IsDirectory: true);
            FileChanged?.Invoke(ev);
            return;
        }

        // Handle files by extension
        var ext = Path.GetExtension(e.FullPath);
        if (PathUtils.IsAudioFile(ext) || PathUtils.IsImage(ext) || PathUtils.IsPlaylist(ext))
        {
            if (TagWriter.OwnedFileWrites.TryGetValue(e.FullPath, out var time)
                && DateTime.Now - time < TimeSpan.FromSeconds(3))
                return;
            
            var ev = new FileChangeEvent(e.FullPath, e.ChangeType, IsDirectory: false);
            FileChanged?.Invoke(ev);
        }
    }


    public void Start() => _watcher.EnableRaisingEvents = true;
    public void Stop() => _watcher.EnableRaisingEvents = false;

    public void Dispose() => _watcher.Dispose();
}

// Simple DTO for FS changes
public record FileChangeEvent(string Path, WatcherChangeTypes ChangeType, bool Overflow = false, bool IsDirectory = false);
