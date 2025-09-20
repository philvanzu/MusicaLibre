using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using MusicaLibre.Services;

namespace MusicaLibre.Models;

public class DiscardedFile
{
    public long DatabaseIndex {get; set;}
    public string FilePath { get; set; }
    public string? Error { get; set; }

    private const string insertSql = @"
        INSERT INTO DiscardedFiles (FilePath, Error) VALUES (@FilePath, @Error);
        SELECT last_insert_rowid();";
    
    private const string selectSql = @"SELECT * FROM DiscardedFiles;";
    private const string deleteSql = @"DELETE FROM DiscardedFiles WHERE Id = @Id;";
    private Dictionary<string, object?> Parameters => new (){
        ["@Id"] = DatabaseIndex,
        ["@FilePath"] = FilePath,
        ["@Error"] = Error,
    };
    
    public DiscardedFile(){}
    public DiscardedFile(string path, string? error=null)
    {
        FilePath = path;
        Error = error;
    }
    
    public void DbInsert(Database db)
    {
        var id= db.ExecuteScalar(insertSql, Parameters);
        DatabaseIndex = Convert.ToInt64(id);
    }
    public async Task DbInsertAsync(Database db)
    {
        var id = await db.ExecuteScalarAsync(insertSql, Parameters);
        DatabaseIndex = Convert.ToInt64(id);
    }
    
    public async Task DbDeleteAsync(Database db) 
        => await db.ExecuteNonQueryAsync(deleteSql, Parameters);
    
    public static Dictionary<string, DiscardedFile> DbSelect(Database db)
        => ProcessReaderResults(db.ExecuteReader(selectSql));
    public static async Task<Dictionary<string, DiscardedFile>> DbSelectAsync(Database db)
        => ProcessReaderResults(await db.ExecuteReaderAsync(selectSql));

    private static Dictionary<string, DiscardedFile> ProcessReaderResults(List<Dictionary<string, object?>> results)
    {
        var discards = new Dictionary<string, DiscardedFile>();
        foreach (var row in results)
        {
            var entry = new DiscardedFile()
            {
                FilePath = (string?)row["FilePath"]??string.Empty,
                Error = (string?)row["Error"]??string.Empty
            };
            discards.Add(entry.FilePath, entry);
        }
        return discards;
    }

}