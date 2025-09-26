using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.Views;

namespace MusicaLibre.ViewModels;

public partial class TracksListTranscoderViewModel:TracksListViewModel
{
    public TracksListTranscoderDialog Window {get;init;}
    public string[] OutputFormats => EnumUtils.GetDisplayNames<AudioTranscoder.OutputFormat>();
    [ObservableProperty] private int _selectedOutputFormatIdx = 1;
    [ObservableProperty] private bool _deleteSourceFiles;
    [ObservableProperty] private string _prefix= string.Empty;
    [ObservableProperty] private string _suffix = string.Empty;
    [ObservableProperty] private string _subDirectory = "MusicaLibre_Transcoded";
    [ObservableProperty] private string _executeButtonText = "Start transcoding";
    [ObservableProperty] private bool _canExecute;
    [ObservableProperty] private ObservableCollection<ProgressViewModel> _progresses = new();
    [ObservableProperty] private string? _commonRoot = string.Empty;
    private SemaphoreSlim _throttler = new SemaphoreSlim(Environment.ProcessorCount);
    public TracksListTranscoderViewModel(LibraryViewModel library, List<Track> tracksPool, TracksListTranscoderDialog window) :
        base(library, tracksPool)
    {
        Window = window;
        window.Closing += OnWindowClosing;

        _columns = new List<TrackViewColumn>()
        {
            new("Title", TrackSortKeys.Title, t => t.Model.Title??string.Empty, this)
            {
                ToolTipGetter = track => track.Model.FilePath,
            },
        };

        UpdateCollection();
    }

    protected override void SelectedTrackChanged()
    {
        CanExecute = (SelectedItems.Count > 0);
        ExecuteButtonText = (SelectedItems.Count <= 1)? "Start transcoding":"Start transcoding batch";

        List<string> dirs = new();
        foreach (var item in SelectedItems)
        {
            var track = item.Model;
            var directory = Path.GetDirectoryName(track.FilePath);
            if(!string.IsNullOrEmpty(directory))
                dirs.Add(directory);
            CommonRoot = PathUtils.GetCommonRoot(dirs);
             
        }
        
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        
    }

    [RelayCommand]
    async Task Execute()
    {
        var outputExtension = SelectedOutputFormatIdx switch
        {
            0 => ".flac",
            _ => ".mp3"
        };
        var outputFormat = SelectedOutputFormatIdx switch
        {
            0 => AudioTranscoder.OutputFormat.Flac,
            _ => AudioTranscoder.OutputFormat.Mp3
        };
        
        Progresses.Clear();
        List<Task> tasks = new();
        
        foreach (var vm in SelectedItems)
        {
            var track = vm.Model;

            var filename = $"{Path.GetFileNameWithoutExtension(track.FilePath)}";
            if (track.Start != 0 || track.End != 1)
                filename = $"{track.DiscNumber}_{track.TrackNumber}_{track.Title}";
            filename = $"{Prefix}{filename}{Suffix}{outputExtension}";
            
            var directory = Path.GetDirectoryName(track.FilePath);
            
            var outputDirectory = Path.Combine(directory, SubDirectory);
            if (!Directory.Exists(outputDirectory))  Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory,  filename);

            var progress = new ProgressViewModel();
            Progresses.Add(progress);
            tasks.Add( EnqueueJob(track, outputPath, outputFormat, progress));
        }
        await Task.WhenAll(tasks);
        foreach (var progress in Progresses)
        {
            progress.Progress.Report(("", -1, true));
            progress.Dispose();
        }

        
        Progresses.Clear();
    }

    [RelayCommand]
    void OpenInExplorer()
    {
        if(!string.IsNullOrEmpty(CommonRoot) && Directory.Exists(CommonRoot))
            PathUtils.OpenInExplorer(CommonRoot);   
    }

    async Task EnqueueJob(Track track, string output, AudioTranscoder.OutputFormat format, ProgressViewModel progress)
    {
        await _throttler.WaitAsync();
        try
        {
            if (await AudioTranscoder.ConvertAsync(track, output, format, progress))
            {
                try
                {
                    File.SetCreationTime(output, track.Created);
                    File.SetLastWriteTime(output, track.Modified); 
                }
                catch (Exception ex){Console.WriteLine(ex);}
            }
            else 
                DialogUtils.MessageBox(Window, 
                    "Error", $"{track.Title} : failed to convert {output} to {format.ToString()}");
        }
        finally
        {
            _throttler.Release();
        }
    }

}