using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;
using TagLib;
using File = System.IO.File;

namespace MusicaLibre.Services;

public class Database
{
    
    public string Path { get; init; }
    // Connection string
    string ConnectionString => $"Data Source={Path}";
    private SqliteConnection? _connection;
    private SqliteTransaction? _transaction;
    private bool _ownsConnection = false;
    public int? TransactionCounter { get; set; }
    private bool IsOpen=>_connection!=null && _connection.State == ConnectionState.Open;
    private bool _asyncMode = false;

    public bool IsAsync => _asyncMode;
    // Signal to wake the worker when new work arrives
    private readonly SemaphoreSlim _signal = new(0);
    private Task? _workerTask;
    CancellationTokenSource? _cts;

    // Work queues
    private readonly ConcurrentQueue<DbWork<int>> _nonQueryQueue = new();      // INSERT/UPDATE/DELETE
    private readonly ConcurrentQueue<DbWork<object?>> _scalarQueue = new();   // SELECT COUNT(*), etc
    private readonly ConcurrentQueue<DbWork<List<Dictionary<string, object?>>>> _readerQueue = new(); // SELECT rows
    private record DbWork<T>(
        string Sql,
        Dictionary<string, object?>? Params,
        TaskCompletionSource<T> Tcs
    );
    
    public Database(string path)
    {
        Path = path;
    }
    
    public async Task SetModeAsync(bool asyncMode)
    {
        if (asyncMode)
        {
            if (_asyncMode) return; // already in async mode
            _asyncMode = true;
            // Make sure previous worker has finished
            if (_workerTask != null)
                await _workerTask;
            _cts = new CancellationTokenSource();
            _workerTask = WorkerLoop(_cts.Token);
        }
        else
        {
            if (!_asyncMode) return; // already in sync mode
            _asyncMode = false;
            if (_cts != null)
            {
                _cts.Cancel();
                if (_workerTask != null)
                    await _workerTask;
                _cts.Dispose();
                _cts = null;
                _workerTask = null;
            }
        }
    }

    private async Task WorkerLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // wait until *any* queue has work
            await _signal.WaitAsync(token);
            try
            {
                _connection = new SqliteConnection(ConnectionString);
                _connection.Open();

                // drain nonqueries in one transaction if more than one
                if (!_nonQueryQueue.IsEmpty)
                {
                    while (_nonQueryQueue.TryDequeue(out var work))
                    {
                        try
                        {
                            int result = _ExecuteNonQuery(work.Sql, work.Params);
                            work.Tcs.TrySetResult(result);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            work.Tcs.TrySetException(ex);
                        }
                    }
                }

                // drain scalars
                if (!_scalarQueue.IsEmpty)
                {
                    while (_scalarQueue.TryDequeue(out var workS))
                    {
                        try
                        {
                            object? result = _ExecuteScalar(workS.Sql, workS.Params);
                            workS.Tcs.TrySetResult(result);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            workS.Tcs.TrySetException(ex);
                        }
                    }
                }

                if (!_readerQueue.IsEmpty)
                {
                    // drain readers
                    while (_readerQueue.TryDequeue(out var workR))
                    {
                        try
                        {
                            var rows = _ExecuteReader(workR.Sql, workR.Params);
                            workR.Tcs.TrySetResult(rows);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            workR.Tcs.TrySetException(ex);
                        }
                    }    
                }
            }
            catch (Exception ex) { Console.Error.WriteLine("Database Worker Loop error: " + ex); }
            finally
            {
                _connection?.Close();
                _connection?.Dispose();
                _connection = null;
            }
        }
    }    
    public void Open()
    {
        if(_asyncMode)
            throw new NotSupportedException("No Sync operations, Database is in async mode.");
        _Open();
    }

    private void _Open()
    {
        if (!IsOpen)
        {
            _connection = new SqliteConnection(ConnectionString);
            _connection.Open();
            _ownsConnection = true;
        }
    }

    public void Close()
    {
        if(_asyncMode)
            throw new NotSupportedException("No Sync operations, Database is in async mode.");
        _Close();
    }

    private void _Close()
    {
        if (_ownsConnection && IsOpen)
        {
            _connection!.Close();
            _connection.Dispose();
            _connection = null;
            _ownsConnection = false;
        }
    }
    
    private TResult WithConnection<TResult>(Func<SqliteConnection, SqliteTransaction?, TResult> action)
    {
        bool opened = false;
        if (!IsOpen)
        {
            _connection = new SqliteConnection(ConnectionString);
            _connection.Open();
            opened = true;
        }

        try
        {
            // Pass current transaction to the action
            return action(_connection!, _transaction);
        }
        finally
        {
            if (opened)
            {
                _connection!.Close();
            }
        }
    }


    public void BeginTransaction()
    {
        if(_asyncMode)
            throw new NotSupportedException("No Sync operations, Database is in async mode."); 
        _BeginTransaction();
    }

    private void _BeginTransaction()
    {
        if(!IsOpen) throw new InvalidOperationException("Cannot Start transactions when the database is not open");
        _transaction = _connection.BeginTransaction();
        TransactionCounter = 0;
    }
    public void Commit()
    {
        if(_asyncMode)
            throw new NotSupportedException("No Sync operations, Database is in async mode."); 
        _Commit();
    }

    private void _Commit()
    {
        _transaction?.Commit();
        CloseTransaction();
    }

    public void Rollback()
    {
        if(_asyncMode)
            throw new NotSupportedException("No Sync operations, Database is in async mode."); 
        _Rollback();
    }

    private void _Rollback()
    {
        _transaction?.Rollback();
        CloseTransaction();
    }

    void CloseTransaction()
    {
        _transaction?.Dispose();
        _transaction = null;
        TransactionCounter = null;  
    }

    public int ExecuteNonQuery(string sql, Dictionary<string, object?>? parameters = null, bool awaitIfNeeded = true)
    {
        if (_asyncMode)
        {
            if(awaitIfNeeded)ExecuteNonQueryAsync(sql, parameters).Wait(); //blocking the current thread
            else _ = ExecuteNonQueryAsync(sql, parameters); // fire and forget
        }
            
        return _ExecuteNonQuery(sql, parameters);
    }

    public async Task<int> ExecuteNonQueryAsync(string sql, Dictionary<string, object?>? parameters = null)
    {
        if(!_asyncMode)throw new NotSupportedException("No ASync operations, Database is in sync mode.");
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new DbWork<int>(sql, parameters, tcs);

        _nonQueryQueue.Enqueue(request);
        _signal.Release(); // notify worker
        int result = await tcs.Task;
        
        return result;
    }
    private int _ExecuteNonQuery(string sql, Dictionary<string, object?>? parameters = null) =>
        WithConnection((conn, tx) =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (tx != null) cmd.Transaction = tx;

            if (parameters != null)
                foreach (var kv in parameters)
                    cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);

            // Only increment counter if inside a transaction
            if (tx != null)
                TransactionCounter = (TransactionCounter ?? 0) + 1;

            return cmd.ExecuteNonQuery();
        });

    public object? ExecuteScalar(string sql, Dictionary<string, object?>? parameters = null)
    {
        if(_asyncMode)
        {
            Console.WriteLine("Database is in async mode. awaiting ExecuteScalarAsync");
            return ExecuteScalarAsync(sql, parameters).Result;
        } 
        return _ExecuteScalar(sql, parameters);
    }

    public async Task<object?> ExecuteScalarAsync(string sql, Dictionary<string, object?>? parameters = null)
    {
        if(!_asyncMode)
            throw new NotSupportedException("No ASync operations, Database is in sync mode.");
        
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new DbWork<object?>(sql, parameters, tcs);

        _scalarQueue.Enqueue(request);
        _signal.Release(); // notify worker
        var result = await tcs.Task;
        return result;
    }
    
    private object? _ExecuteScalar(string sql, Dictionary<string, object?>? parameters = null) =>
        WithConnection((conn, tx) =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (tx != null) cmd.Transaction = tx;

            if (parameters != null)
                foreach (var kv in parameters)
                    cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);

            if (tx != null)
                TransactionCounter = (TransactionCounter ?? 0) + 1;

            return cmd.ExecuteScalar();
        });


    public List<Dictionary<string, object?>> ExecuteReader(
        string sql,
        Dictionary<string, object?>? parameters = null)
    {
        if(_asyncMode)
        {
            Console.WriteLine("Database is in async mode. awaiting ExecuteReaderAsync");
            return ExecuteReaderAsync(sql, parameters).Result;
        } 
        return  _ExecuteReader(sql, parameters);
    }
    public async Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(
        string sql, 
        Dictionary<string, object?>? parameters = null)
    {
        if(!_asyncMode)throw new NotSupportedException("No ASync operations, Database is in sync mode.");
        var tcs = new TaskCompletionSource<List<Dictionary<string, object?>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new DbWork<List<Dictionary<string, object?>>>(sql, parameters, tcs);

        _readerQueue.Enqueue(request);
        _signal.Release(); // notify worker
        var result = await tcs.Task;
        return result;
    }
    private List<Dictionary<string, object?>> _ExecuteReader(
        string sql,
        Dictionary<string, object?>? parameters = null)
    {
        return WithConnection((conn, tx) =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (tx != null) cmd.Transaction = tx;

            if (parameters != null)
                foreach (var kv in parameters)
                    cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);

            // Only increment counter if inside a transaction
            if (tx != null)
                TransactionCounter = (TransactionCounter ?? 0) + 1;

            using var reader = cmd.ExecuteReader();
            var result = new List<Dictionary<string, object?>>();
            while (reader.Read())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                result.Add(row);
            }
            return result;
        });
    }
    public static T? GetValue<T>(Dictionary<string, object?> row, string key) where T : struct
    {
        var value = row[key];
        if (value == null || value is DBNull) return null;
        return (T)Convert.ChangeType(value, typeof(T));
    }

    public static string? GetString(Dictionary<string, object?> row, string key)
    {
        var value = row[key];
        return value == null || value is DBNull ? null : (string)value;
    }
    
    public static T? GetEnum<T>(Dictionary<string, object?> row, string key) where T : struct, Enum
    {
        var value = row[key];
        if (value == null || value is DBNull) return null;

        // Convert to the underlying type first (usually int)
        return (T)Enum.ToObject(typeof(T), Convert.ToInt32(value));
    }
    public static byte[]? GetBlob(Dictionary<string, object?> row, string key)
    {
        var value = row[key];
        if (value == null || value is DBNull) return null;
        return (byte[])value;
    }
    public static Database? Create(DirectoryInfo libraryRoot)
    {
        var fileName = System.IO.Path.GetFileName($"{libraryRoot.Name}.db");
        var path = System.IO.Path.Combine(AppData.Path, fileName);
        if (!File.Exists(path))
        {
            Database db = new Database(path);
            try
            {
                using var connection = new SqliteConnection(db.ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = CreateTables;
                command.ExecuteNonQuery();
                
                var settings = JsonSerializer.Serialize(new LibrarySettingsViewModel());
                var sql = $"INSERT INTO Info (DBPath, LibraryRoot, Added, Settings) Values ($dbpath, $libraryroot, $added, $settings)";
                db.ExecuteNonQuery(sql, new ()
                {
                    ["$dbpath"] = path,
                    ["$libraryroot"] = libraryRoot.FullName,
                    ["$added"] = DateTime.Now,
                    ["$settings"] = settings,
                });
                return db;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                if(File.Exists( path))
                    File.Delete(path);
            }
        }
        throw new IOException("Database file already exists");
    }




    private const string CreateTables = @"
PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS Info (
    DBPath TEXT,
    LibraryRoot TEXT,
    Added INTEGER,
    Settings TEXT
);
-- =========================
-- Tracks
-- =========================
CREATE TABLE IF NOT EXISTS Tracks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FilePath TEXT NOT NULL,
    FileName TEXT NOT NULL,
    FolderId INTEGER NOT NULL,
    FileExtension TEXT NOT NULL,
    Title TEXT NOT NULL,
    YearId INTEGER NOT NULL,
    TrackNumber INTEGER NOT NULL,
    DiscNumber INTEGER NOT NULL,
    Duration REAL NOT NULL, -- seconds
    Start REAL DEFAULT 0,
    End REAL DEFAULT 1,
    Bitrate INTEGER NOT NULL,
    Codec TEXT  NOT NULL,
    SampleRate INTEGER NOT NULL,
    Channels INTEGER NOT NULL,
    Added INTEGER NOT NULL, -- Unix timestamp
    Modified INTEGER NOT NULL, -- Unix timestamp
    Created INTEGER NOT NULL, -- Unix timestamp
    LastPlayed INTEGER, -- Unix timestamp
    HasEmbeddedCover INTEGER DEFAULT 0, -- 0=false, 1=true
    AlbumId INTEGER,
    PublisherId INTEGER,
    ConductorId INTEGER,
    RemixerId INTEGER,
    AudioFormatId INTEGER NOT NULL,
    Comments TEXT NOT NULL,
    Rating REAL,
    PlayCount INTEGER,
    FOREIGN KEY (AlbumId) REFERENCES Albums(Id) ON DELETE SET NULL,
    FOREIGN KEY (PublisherId) REFERENCES Publishers(Id) ON DELETE SET NULL,
    FOREIGN KEY (ConductorId) REFERENCES Artists(Id) ON DELETE SET NULL,
    FOREIGN KEY (RemixerId) REFERENCES Artists(Id) ON DELETE SET NULL,
    FOREIGN KEY (AudioFormatId) REFERENCES AudioFormats(Id) ON DELETE RESTRICT,
    FOREIGN KEY (YearId) REFERENCES Years(Id) ON DELETE RESTRICT,
    FOREIGN KEY (FolderId) REFERENCES Folders(Id) ON DELETE CASCADE
);

-- =========================
-- Albums
-- =========================
CREATE TABLE IF NOT EXISTS Albums (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    AlbumArtist INT NOT NULL,
    YearId INTEGER NOT NULL,
    FolderId INTEGER NOT NULL,
    Added INTEGER NOT NULL, -- Unix timestamp
    Modified INTEGER NOT NULL, -- Unix timestamp
    Created INTEGER NOT NULL, -- Unix timestamp
    LastPlayed INTEGER, -- Unix timestamp
    CoverId INTEGER,
    FOREIGN KEY (AlbumArtist) REFERENCES Artists(Id) ON DELETE RESTRICT,
    FOREIGN KEY (CoverId) REFERENCES Artworks(Id) ON DELETE SET NULL,
    FOREIGN KEY (YearId) REFERENCES Years(Id) ON DELETE RESTRICT,
    FOREIGN KEY (FolderId) REFERENCES Folders(Id) ON DELETE CASCADE
);
-- =========================
-- Discs
-- =========================
CREATE TABLE IF NOT EXISTS Discs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AlbumId INTEGER NOT NULL,
    Number INTEGER NOT NULL,
    Name TEXT,
    ArtworkId INTEGER,
    FOREIGN KEY (AlbumId) REFERENCES Albums(Id) ON DELETE CASCADE,
    FOREIGN KEY (ArtworkId) REFERENCES Artworks(Id) ON DELETE CASCADE
);

-- =========================
-- Artists
-- =========================
CREATE TABLE IF NOT EXISTS Artists (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    ArtworkId INTEGER,
    FOREIGN KEY (ArtworkId) REFERENCES Artworks(Id) ON DELETE SET NULL
);

-- =========================
-- Genres
-- =========================
CREATE TABLE IF NOT EXISTS Genres (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    ArtworkId INTEGER,
    FOREIGN KEY (ArtworkId) REFERENCES Artworks(Id) ON DELETE SET NULL
);
-- =========================
-- Publishers
-- =========================
CREATE TABLE IF NOT EXISTS Publishers (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    ArtworkId INTEGER,
    FOREIGN KEY (ArtworkId) REFERENCES Artworks(Id) ON DELETE SET NULL
);
-- =========================
-- Artworks
-- =========================
CREATE TABLE Artworks (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Hash        TEXT NOT NULL UNIQUE,       -- SHA256 or MD5 of the thumbnail bytes
    SourcePath  TEXT NOT NULL,
    FolderId    INTEGER NOT NULL,
    SourceType  INT NOT NULL,
    Width       INTEGER NOT NULL,
    Height      INTEGER NOT NULL,
    Thumbnail   BLOB NOT NULL,          -- 200x200 thumbnail bytes
    MimeType    TEXT NOT NULL,          -- e.g., 'image/png'
    Role        INT,
    EmbedIdx    INT,
    BookletPage INT
);
-- =========================
-- Formats
-- =========================
CREATE TABLE IF NOT EXISTS AudioFormats (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE
);
-- =========================
-- Years
-- =========================
CREATE TABLE IF NOT EXISTS Years (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Number INT NOT NULL UNIQUE
);
-- =========================
-- Folders
-- =========================
CREATE TABLE IF NOT EXISTS Folders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE
);



-- =========================
-- Track ↔ Artist (many-to-many)
-- =========================
CREATE TABLE IF NOT EXISTS TrackArtists (
    TrackId INTEGER NOT NULL,
    ArtistId INTEGER NOT NULL,
    PRIMARY KEY (TrackId, ArtistId),
    FOREIGN KEY (TrackId) REFERENCES Tracks(Id) ON DELETE CASCADE,
    FOREIGN KEY (ArtistId) REFERENCES Artists(Id) ON DELETE CASCADE
);

-- =========================
-- Track ↔ Composers (many-to-many)
-- =========================
CREATE TABLE IF NOT EXISTS TrackComposers (
    TrackId INTEGER NOT NULL,
    ArtistId INTEGER NOT NULL,
    PRIMARY KEY (TrackId, ArtistId),
    FOREIGN KEY (TrackId) REFERENCES Tracks(Id) ON DELETE CASCADE,
    FOREIGN KEY (ArtistId) REFERENCES Artists(Id) ON DELETE CASCADE
);

-- =========================
-- Track ↔ Genre (many-to-many)
-- =========================
CREATE TABLE IF NOT EXISTS TrackGenres (
    TrackId INTEGER NOT NULL,
    GenreId INTEGER NOT NULL,
    PRIMARY KEY (TrackId, GenreId),
    FOREIGN KEY (TrackId) REFERENCES Tracks(Id) ON DELETE CASCADE,
    FOREIGN KEY (GenreId) REFERENCES Genres(Id) ON DELETE CASCADE
    );
    
-- =========================
-- Track ↔ Artwork (many-to-many)
-- =========================
CREATE TABLE IF NOT EXISTS TrackArtworks (
    TrackId INTEGER NOT NULL,
    ArtworkId INTEGER NOT NULL,
    PRIMARY KEY (TrackId, ArtworkId),
    FOREIGN KEY (TrackId) REFERENCES Tracks(Id) ON DELETE CASCADE,
    FOREIGN KEY (ArtworkId) REFERENCES Artworks(Id) ON DELETE CASCADE
    );
    
-- =========================
-- Album ↔ Artwork (many-to-many)
-- =========================
CREATE TABLE IF NOT EXISTS AlbumArtworks (
    AlbumId INTEGER NOT NULL,
    ArtworkId INTEGER NOT NULL,
    PRIMARY KEY (AlbumId, ArtworkId),
    FOREIGN KEY (AlbumId) REFERENCES Albums(Id) ON DELETE CASCADE,
    FOREIGN KEY (ArtworkId) REFERENCES Artworks(Id) ON DELETE CASCADE
    );
    
-- =========================
-- Playlists
-- =========================
CREATE TABLE IF NOT EXISTS Playlists (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FilePath TEXT NOT NULL,
    FileName TEXT NOT NULL,
    FolderId INTEGER NOT NULL,
    Created INTEGER,
    Modified INTEGER,
    ArtworkId INTEGER,
    FOREIGN KEY (ArtworkId) REFERENCES Artworks(Id) ON DELETE SET NULL
);

-- =========================
-- Playlist ↔ Track
-- =========================
CREATE TABLE IF NOT EXISTS PlaylistTracks (
    PlaylistId INTEGER NOT NULL,
    TrackId INTEGER NOT NULL,
    Position INTEGER NOT NULL,
    PRIMARY KEY (PlaylistId, TrackId),
    FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE,
    FOREIGN KEY (TrackId) REFERENCES Tracks(Id) ON DELETE CASCADE
);

-- =========================
-- Indexes for performance
-- =========================
CREATE INDEX IF NOT EXISTS idx_tracks_album ON Tracks(AlbumId);
CREATE INDEX IF NOT EXISTS idx_tracks_year ON Tracks(YearId);
CREATE INDEX IF NOT EXISTS idx_trackartists_artist ON TrackArtists(ArtistId);
CREATE INDEX IF NOT EXISTS idx_trackgenres_genre ON TrackGenres(GenreId);
";

}