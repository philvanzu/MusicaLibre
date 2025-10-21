using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.Views;

namespace MusicaLibre.ViewModels;

public partial class  PublishersEditorViewModel:ViewModelBase, IDisposable
{
    LibraryViewModel _library;
    [ObservableProperty] ObservableCollection<Publisher> _publishers;
    [ObservableProperty] string _poolFilter;
    [ObservableProperty] int _selectedPublisherIndex;
    [ObservableProperty] Publisher? _selectedPublisher;
    [ObservableProperty] private ObservableCollection<string?> _selectedList=new ObservableCollection<string?>();
    
    List<Track>? _selectedPublisherTracks;
    [ObservableProperty] private string? _text;
    
    public Bitmap? Thumbnail => SelectedPublisher?.Artwork?.Thumbnail;
    public Publisher? _oldPublisher;
    private TagsEditorDialog _window;

    public PublishersEditorViewModel(LibraryViewModel library, TagsEditorDialog dialog)
    {
        _window = dialog;
        _library = library;
        _publishers = new (_library.Data.Publishers.Values.OrderBy(x => x.Name).ToList());
    }
    
    partial void OnPoolFilterChanged(string value)
    {
        _publishers.Clear();
        _publishers.AddRange(_library.Data.Publishers.Values.Where(x => x.Name.Contains(value, StringComparison.OrdinalIgnoreCase)));
        SelectedPublisherIndex = -1;
    }
    partial void OnSelectedPublisherIndexChanged(int value)
    {
        if(_oldPublisher != null)
            _oldPublisher.Artwork?.ReleaseThumbnail(this);
        
        if (value >= 0 && value < Publishers.Count)
        {
            SelectedPublisher = _oldPublisher =Publishers[value];
            Text = SelectedPublisher.Name;
            SelectedPublisher.Artwork?.RequestThumbnail(this, ()=>OnPropertyChanged(nameof(Thumbnail)));
            _selectedPublisherTracks = _library.Data.Tracks.Values.Where(x=>x.Publisher==SelectedPublisher).ToList();
            SelectedList.Clear();
            SelectedList.AddRange(_selectedPublisherTracks.Select(x=> x.FilePath));
        }
        else
        {
            SelectedPublisher = null;
            _selectedPublisherTracks = null;
        }
        OnPropertyChanged(nameof(Thumbnail));
    }
    public void Dispose()
    {
        SelectedPublisher?.Artwork?.ReleaseThumbnail(this);
    }

    [RelayCommand]
    async Task PickArtwork()
    {
        if (SelectedPublisher == null) return;
        var artwork = await DialogUtils.PickArtwork(_window, _library, _library.Settings.PublisherArtworkPath,
            ArtworkRole.Other);
        if (artwork == null) return;
        SelectedPublisher?.Artwork?.ReleaseThumbnail(this);
        
        SelectedPublisher!.Artwork = artwork;
        await SelectedPublisher.DbUpdateAsync(_library.Database);
        
        SelectedPublisher.Artwork.RequestThumbnail(this, () => OnPropertyChanged(nameof(Thumbnail)));
    }

    [RelayCommand]
    async Task Rename()
    {
        if (string.IsNullOrWhiteSpace(Text)) return; 
        
        string rename = Text;
        
        var existing = _library.Data.Publishers.Values.FirstOrDefault(x => x.Name == rename);
        HashSet<Track> dirtyTracks = new();
        if (existing != null && _selectedPublisherTracks != null)
        {
            foreach (var track in _selectedPublisherTracks)
            {
                if (track.Publisher != existing)
                {
                    track.Publisher = existing;
                    dirtyTracks.Add(track);
                }
            }
            if (dirtyTracks.Count > 0)
            {
                var progress = new ProgressDialogViewModel();
                Dispatcher.UIThread.Post(() => _ = progress.Show(), DispatcherPriority.Render);
                
                await progress.DialogShown;
                await Task.Delay(100);
            
            
                List<Task> tasks = new();
                var total = dirtyTracks.Count;
                var current = 0;
                foreach (var track in dirtyTracks)
                {
                    tasks.Add(track.DbUpdateAsync(_library.Database));
                    tasks.Add(track.UpdateArtistsAsync(_library));
                    TagWriter.EnqueueFileUpdate(track);
                
                    if (tasks.Count > 8)
                    {
                        progress.Progress.Report(("Updating tracks", (double)current/total, false));
                        await Task.WhenAll(tasks);
                        tasks.Clear();
                    }
                    current++;
                }
                progress.Progress.Report(("Updating tracks", (double)current/total, false));
                await Task.WhenAll(tasks);
                tasks.Clear();
                progress.Progress.Report(("done", 1, true));
                progress.Dispose();
            }
            
            var message = $"{dirtyTracks.Count} tracks updated.";
            await DialogUtils.MessageBox(_window, "Success", message);
        }
        else
        {
            SelectedPublisher!.Name = rename;
            await SelectedPublisher.DbUpdateAsync(_library.Database);
            await DialogUtils.MessageBox(_window, "Success", $"Renamed one publisher");
        }
    }

    [RelayCommand]
    async Task Delete()
    {
        if (SelectedPublisher is null) return;
        HashSet<Track> dirtyTracks = new();
        
        foreach (var track in _selectedPublisherTracks     )
        {
            track.Publisher = null;
            dirtyTracks.Add(track);
        }
    }

    [RelayCommand]
    void ArrowUp()
    {
        var index = SelectedPublisherIndex - 1;
        if (index >=Publishers.Count || index < 0) index = Publishers.Count-1;
        SelectedPublisherIndex = index;
    }

    [RelayCommand]
    void ArrowDown()
    {
        var index = SelectedPublisherIndex + 1;
        if (index >=Publishers.Count || index < 0) index = 0;
        SelectedPublisherIndex = index;
    }
}