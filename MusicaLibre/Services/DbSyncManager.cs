using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MusicaLibre.Models;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Services;

public class DbSyncManager: IDisposable
{
    private readonly LibraryViewModel _library;

    public bool IsRunning { get; private set; }

    private Task? _service;
    private Task? _sync;
    public Task? SyncTask => _sync;
    private FileSystemWatcherService _watcher;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentQueue<FileChangeEvent> _fsBuffer = new();

    private readonly TimeSpan _debounceDelay = TimeSpan.FromSeconds(5);
    private DateTime _lastEventTime = DateTime.MinValue;

    private readonly SemaphoreSlim _syncLock = new(1,1);
    public SemaphoreSlim SyncLock => _syncLock;
    public DbSyncManager(LibraryViewModel library)
    {
        _library = library;
        _watcher = new FileSystemWatcherService(library.Path, OnFileSystemEvent);
    }

    public void StartSyncService()
    {
        if (IsRunning) return;
        IsRunning = true;

        _cts = new CancellationTokenSource();
        _service = Task.Run(() => RunServiceAsync(_cts.Token));
    }

    public async Task StopSyncServiceAsync()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        
        if (_service != null) await _service;
        IsRunning = false;
    }

    private async Task RunServiceAsync(CancellationToken token)
    {
        // Step 1: Run startup full sync
        await RunFullSyncAsync(token);
        _watcher.Start();
        // Step 2: Start listening loop
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(1000, token); // poll loop

            if (_lastEventTime != DateTime.MinValue &&
                DateTime.UtcNow - _lastEventTime >= _debounceDelay)
            {
                // Clear buffer
                while (_fsBuffer.TryDequeue(out _)) { }

                _lastEventTime = DateTime.MinValue;

                // Trigger a new full sync
                await RunFullSyncAsync(token);
            }
        }
        _watcher.Stop();
    }

    private async Task RunFullSyncAsync(CancellationToken token)
    {
        await _syncLock.WaitAsync(token);
        try
        {
            // Avoid starting multiple syncs
            if (_sync is { IsCompleted: false })
            {
                await _sync; // wait for it to finish
                return;
            }

            // Run the sync under the same lock
            _sync = Task.Run(() => _library.SyncDatabase(), token);
            await _sync;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    // this will be called by FileSystemWatcher
    public void OnFileSystemEvent(FileChangeEvent ev)
    {
        _fsBuffer.Enqueue(ev);
        _lastEventTime = DateTime.UtcNow;
    }


    public void Dispose()
    {
        if(IsRunning)
            StopSyncServiceAsync().GetAwaiter().GetResult();
        
        _service?.Dispose();
        _sync?.Dispose();
        _watcher.Dispose();
        _cts?.Dispose();
    }
}