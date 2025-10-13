using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.Views;

namespace MusicaLibre.ViewModels;

public partial class PlaylistViewModel:TracksGroupViewModel, IVirtualizableItem
{
    [ObservableProperty] PlaylistsListViewModel _presenter;
    public override List<Track> Tracks=>Model.Tracks.OrderBy(x => x.position).Select(x=>x.track).ToList();
    public string Count => $"{Tracks.Count} tracks"; 
    public Playlist Model { get; init; }
    public string FileName => Model.FileName??"";
    public string FilePath => Model.FilePath??"";
    public Bitmap? Thumbnail => Artwork?.Thumbnail;
    private Window Window => Presenter.Library.MainWindowViewModel.MainWindow;
    [ObservableProperty]Artwork? _artwork;

    public Artwork? FindArtwork()
    {
        if(Model.Artwork != null) return Model.Artwork;
        return Presenter.Library.Data.Artworks.Values.FirstOrDefault(x => x.Folder == Model.Folder);
    }
    public int RandomIndex { get; set; }
    [ObservableProperty] private bool _isSelected;
    partial void OnIsSelectedChanged(bool oldValue, bool newValue)
    {
        if(newValue && Presenter.SelectedItem != this)
            Presenter.SelectedItem = this;
        if (oldValue && Presenter.SelectedItem == this)
            Presenter.SelectedItem = null;
    }

    public bool IsFirst => Presenter.GetItemIndex(this) == 0; 
    public bool IsPrepared { get; set; }

    public PlaylistViewModel(PlaylistsListViewModel presenter, Playlist playlist):base(presenter.Library)
    {
        Model = playlist;
        Presenter = presenter;
        Artwork = FindArtwork();
    }
    public void OnPrepared()
    {
        IsPrepared = true;
        if(Artwork != null)
            Artwork.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Thumbnail)));
    }

    public void OnCleared()
    {
        if(Artwork != null)
            Artwork.ReleaseThumbnail(this);
    }

    [RelayCommand]
    void OpenInExplorer() => PathUtils.OpenInExplorer(FilePath);
    
    [RelayCommand]void Edit(){}

    [RelayCommand]void DoubleTapped()=>Presenter.Library.ChangeOrderingStep(Presenter);

    [RelayCommand] void Play()=>Presenter.Library.NowPlayingList.Replace(Tracks);
    [RelayCommand] void InsertNext()=>Presenter.Library.NowPlayingList.Insert(Tracks);
    [RelayCommand] void Append()=>Presenter.Library.NowPlayingList.Append(Tracks);

    [RelayCommand] async Task PickArtwork()
    {
        var paths = Tracks.Select(x => x.Folder.Name).Distinct();
        var rootDirectory = PathUtils.GetCommonRoot(paths) ?? Presenter.Library.Path;
        var artwork = await DialogUtils.PickArtwork(Window, Presenter.Library, rootDirectory, ArtworkRole.CoverFront);
        if (artwork != null)
        {
            Artwork?.ReleaseThumbnail(this);
            Artwork = Model.Artwork = artwork;
            OnPropertyChanged(nameof(Artwork));
            artwork.RequestThumbnail(this,()=>OnPropertyChanged(nameof(Thumbnail)));
            await Model.DbUpdateAsync(Presenter.Library.Database);  
        }
    }

    [RelayCommand] async Task Delete()
    {
        if(await DialogUtils.YesNoDialog(Window, "Delete",$"Delete {Model.FilePath}?"))
        {
            File.Delete(Model.FilePath);
            Presenter.RemoveItem(this);
        }
    }
    
    public override async Task AddToDevice(ExternalDevice device)
    {
        var win = Library.MainWindowViewModel.MainWindow;

        if (device.Info != null && await ExternalDevicesManager.MountDeviceAsync(device.Info)
            && device.Info.Info != null && !string.IsNullOrEmpty(device.Info.Info.LocalPath))
        {
            if (! await DialogUtils.YesNoDialog(win, 
                    "Copy to Device", 
                    $"Copy {Tracks.Count} tracks to {device.Name}?"))
                return;
            
            try
            {
                var progressvm = new ProgressDialogViewModel();
                var progressdlg = new ProgressDialog(progressvm);
                Dispatcher.UIThread.Post(()=>_ = progressdlg.ShowDialog(win));

                try
                {
                    await progressvm.DialogShown;
                    await Task.Delay(200);
                    
                    var deviceRoot= Path.Combine(device.Info.Info.LocalPath, device.MusicPath);
                    
                    var playlistName = Path.GetFileNameWithoutExtension(Model.FilePath);

                    var dstDir = Path.GetDirectoryName(device.PlaylistSyncRule)??string.Empty;
                    dstDir = dstDir.Replace("$PlaylistName", playlistName);
                    dstDir = Path.Combine(deviceRoot, dstDir);
                    
                    if(!Directory.Exists(dstDir))
                        Directory.CreateDirectory(dstDir);

                    var dstFilename = Path.GetFileNameWithoutExtension(device.PlaylistSyncRule);
                    
                    List<string> dstFiles = new List<string>();
                    
                    var total = Tracks.Count;
                    int i = 1;
                    foreach (var track in Tracks)
                    {
                        var fileName = Path.GetFileName(track.FilePath);
                        var filenameWithoutExtension = Path.GetFileNameWithoutExtension(track.FilePath);
                        var ext = Path.GetExtension(track.FilePath);
                        
                        var srcPath = track.FilePath;
                        string tmpPath = string.Empty;
                        
                        if (track.BitrateKbps > device.MaxBitrate)
                        {
                            tmpPath = Path.Combine("/tmp", $"{filenameWithoutExtension}.mp3");
                            if (await AudioTranscoder.ConvertAsync(track, tmpPath, AudioTranscoder.OutputFormat.Mp3, progressvm))
                            {
                                srcPath = tmpPath;
                                ext = ".mp3";
                            }
                            else
                            {
                                await DialogUtils.MessageBox(win, "Error",  $"MusicaLibre could not transcode {srcPath} to low bitrate format." );
                                continue;
                            }
                        }
                        
                        string filename = dstFilename;
                        filename = filename.Replace("$PlaylistNumber", i.ToString());
                        filename = filename.Replace("$TrackFileName", Path.GetFileNameWithoutExtension(track.FileName));
                        filename = $"{fileName}{ext}";
                        
                        var outputPath = Path.Combine(dstDir,  filename);

                        progressvm.Progress.Report(($"Copying {track.FilePath} to {outputPath}", (double)i++/total, false ));
                        await Utils.CopyFileAsync(srcPath, outputPath);
                        dstFiles.Add(filename);
                        if(tmpPath != string.Empty && File.Exists(tmpPath))
                            File.Delete(tmpPath);
                    }
                    
                    var dstPlaylistName = "{playlistName}.m3u";
                    var tmpPlaylistPath = $"/tmp/{dstPlaylistName}";
                    var dstPlaylistPath = Path.Combine(dstDir, dstPlaylistName);
                    
                    Playlist.CreateM3u(tmpPlaylistPath, dstFiles, true);
                    await Utils.CopyFileAsync(tmpPlaylistPath, dstPlaylistPath);
                    
                    if(File.Exists(tmpPlaylistPath))
                        File.Delete(tmpPlaylistPath);

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                finally
                {
                    progressvm.Progress.Report((string.Empty, 0, true));
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                await ExternalDevicesManager.UnmountDeviceAsync(device.Info);
            }
            return;
        }
        await DialogUtils.MessageBox(win,"Error", "Device could not be mounted." );
    }
}