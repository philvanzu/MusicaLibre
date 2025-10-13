using System.Collections.Generic;
using System.Threading.Tasks;
using MusicaLibre.Models;

namespace MusicaLibre.ViewModels;

public abstract class TracksGroupViewModel:ViewModelBase
{
    public abstract List<Track> Tracks { get; }
    public LibraryViewModel Library { get; init; }

    public TracksGroupViewModel(LibraryViewModel library)
    {
        Library = library;
    }

    public virtual async Task AddToDevice(ExternalDevice device)
    {
        await Task.CompletedTask;
    }
}