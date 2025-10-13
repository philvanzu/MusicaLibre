using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;
using MusicaLibre.Views;

namespace MusicaLibre.ViewModels;

public partial class AlbumViewModel : TracksGroupViewModel, IVirtualizableItem, IDisposable
{
    public Album Model { get; init; }
    public virtual string Title => Model.Title;
    public string Artist => Model.AlbumArtist?.Name??"";
    public string Year => Model.Year?.Name ?? "";

    public string RootFolder => Model.Folder.Name??"";
    public DateTime? Added => Model.Added;
    public DateTime? Modified => Model.Modified;
    public DateTime? Created => Model.Created;
    public DateTime? LastPlayed => Model.LastPlayed;
    public int RandomIndex {get;set;}
    public override List<Track> Tracks => Presenter.TracksPool.Where(x=>x.AlbumId == Model.DatabaseIndex).ToList();
    

    public virtual Artwork? Artwork => Model.Cover;
    public Bitmap? Thumbnail => Artwork?.Thumbnail;

    private bool _suppressSelectedUpdates;
    [ObservableProperty] bool _isSelected;
    partial void OnIsSelectedChanged(bool oldValue, bool newValue)
    {
        if (_suppressSelectedUpdates) return;
        try
        {
            _suppressSelectedUpdates = true;   
            if (newValue && Presenter.SelectedItem != this )
                Presenter.SelectedItem = this;
            else if (oldValue && Presenter.SelectedItem == this)
                Presenter.SelectedItem = null;
        }
        finally
        {
            _suppressSelectedUpdates = false;
        }
    }
    
    public AlbumsListViewModel Presenter { get; init; }
    public bool IsFirst => Presenter.GetItemIndex(this) == 0;
    public bool IsPrepared { get; private set; }

    public AlbumViewModel(AlbumsListViewModel presenter, Album model):base(presenter.Library)
    {
        Presenter = presenter;
        Model = model;
    }

    public void OnPrepared()
    {
        IsPrepared = true;
        GetThumbnail();
    }

    public void OnCleared()
    {
        Artwork?.ReleaseThumbnail(this);
    }

    public void GetThumbnail()
    {
        
        if (Artwork == null) return;
        Artwork.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Thumbnail)));

        
    }

    

    [RelayCommand] void Play()=>Presenter.Library.NowPlayingList.Replace(Tracks);
    [RelayCommand] void InsertNext()=>Presenter.Library.NowPlayingList.Insert(Tracks);
    [RelayCommand] void Append()=>Presenter.Library.NowPlayingList.Append(Tracks);
    [RelayCommand] void OpenInExplorer() { PathUtils.OpenInExplorer(Model.Folder.Name);}

    [RelayCommand] void Delete(){}

    [RelayCommand]
    void EditTags()
    {
        if (Presenter.SelectedTracks != null && Presenter.SelectedTracks.Count > 0)
            _ = Presenter.Library.EditTracks(Presenter.SelectedTracks);
        else
            _ = Presenter.Library.EditTracks(Tracks);

    }

    [RelayCommand]
    void Transcode()
    {
        if (Presenter.SelectedTracks != null && Presenter.SelectedTracks.Count > 0)
            _ = Presenter.Library.TranscodeTracks(Presenter.SelectedTracks);
        else
            _ = Presenter.Library.TranscodeTracks(Tracks);
    }

    [RelayCommand]
    void DoubleTapped()
    {
        Presenter.Library.ChangeOrderingStep(Presenter);
    }

    [RelayCommand]
    async Task PickArtwork()
    {
        await _PickArtwork();
    }

    protected virtual async Task _PickArtwork()
    {
        var paths = Tracks.Select(x => x.Folder?.Name).Distinct();
        var rootDirectory = PathUtils.GetCommonRoot(paths) ?? Presenter.Library.Path;
        var artwork = await DialogUtils.PickArtwork(Presenter.Library.MainWindowViewModel.MainWindow, Presenter.Library, rootDirectory, ArtworkRole.CoverFront);
        if (artwork != null)
        {
            Artwork?.ReleaseThumbnail(this);
            Model.Cover = artwork;
            artwork.RequestThumbnail(this,()=>OnPropertyChanged(nameof(Thumbnail)));
            await Model.DbUpdateAsync(Presenter.Library.Database);  
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
                    
                    var total = Tracks.Count;
                    int i = 1;
                    foreach (var track in Tracks)
                    {
                        var fileName = Path.GetFileName(track.FilePath);
                        var filenameWithoutExtension = Path.GetFileNameWithoutExtension(track.FilePath);
                        var ext = Path.GetExtension(track.FilePath);
                        var dirPath = PathUtils.GetRelativePath(Library.Path,
                            Path.GetDirectoryName(track.FilePath) ?? string.Empty);
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
                        
                        string outputPath = device.AlbumSyncRule;
                        outputPath = outputPath.Replace("$AlbumArtist", track.Album.AlbumArtist.Name);
                        outputPath = outputPath.Replace("$AlbumTitle", track.Album.Title);
                        outputPath = outputPath.Replace("$DiscNumber", track.DiscNumber.ToString());
                        outputPath = outputPath.Replace("$TrackNumber", track.TrackNumber.ToString());
                        outputPath = outputPath.Replace("$TrackTitle", track.Title);

                        outputPath = Path.Combine(deviceRoot,  outputPath);
                        outputPath = $"{outputPath}{ext}";
                        var outputDir = Path.GetDirectoryName(outputPath);
                        if (string.IsNullOrEmpty(outputDir)) continue;
                        if(!Directory.Exists(outputDir))
                            Directory.CreateDirectory(outputDir);

                        progressvm.Progress.Report(($"Copying {track.FilePath} to {outputPath}", (double)i++/total, false ));
                        await Utils.CopyFileAsync(srcPath, outputPath);
                        
                        if(tmpPath != string.Empty && File.Exists(tmpPath))
                            File.Delete(tmpPath);
                    }
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

    public void Dispose()
    {
        Artwork?.ReleaseThumbnail(this);
    }
}

public partial class AlbumDiscViewModel:AlbumViewModel{
    
    public Disc Disc { get; set; }
    public override string Title => string.IsNullOrEmpty(Disc.Name)? base.Title:Disc.Name;
    public override Artwork? Artwork => Disc.Artwork ?? base.Artwork;
    
    public override List<Track> Tracks => 
        (Presenter as DiscsListViewModel)?.TracksPool
        .Where(x => x.DiscNumber == Disc.Number && x.AlbumId == Disc.AlbumId).ToList()
        ??new List<Track>();
    public AlbumDiscViewModel(DiscsListViewModel presenter, Disc model) : base(presenter, model.Album)
    {
        Disc = model;
    }
    
    protected override async Task _PickArtwork()
    {
        var paths = Tracks.Select(x => x.Folder.Name).Distinct();
        var rootDirectory = PathUtils.GetCommonRoot(paths) ?? Presenter.Library.Path;
        var artwork = await DialogUtils.PickArtwork(Presenter.Library.MainWindowViewModel.MainWindow, Presenter.Library, rootDirectory, ArtworkRole.CoverFront);
        if (artwork != null)
        {
            Artwork?.ReleaseThumbnail(this);
            Disc.Artwork = artwork;
            OnPropertyChanged(nameof(Artwork));
            artwork.RequestThumbnail(this,()=>OnPropertyChanged(nameof(Thumbnail)));
            await Disc.DbUpdateAsync(Presenter.Library.Database);  
        }
    }


}