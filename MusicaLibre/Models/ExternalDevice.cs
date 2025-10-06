using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Models;


public partial class ExternalDevice : ViewModelBase , IDisposable
{
    
    public MtpDeviceInfo? Info { get; set; }
    [ObservableProperty] private string _name = string.Empty;
    
    [ObservableProperty] private bool _isPlugged = false;
    [ObservableProperty] private string _musicPath = string.Empty;
    [ObservableProperty] private int _maxBitrate = 320;
    [ObservableProperty] private string _syncRule = "$AlbumArtist/$AlbumTitle/$DiscNumber - $TrackNumber_$TrackTitle";
    public DateTime LastSeen {get; set;}
    [JsonIgnore] public AppSettingsViewModel? Presenter{get; set;}
    public ExternalDevice() {}
    public ExternalDevice(MtpDeviceInfo info)
    {
        Info = info;
        Name = info.Name;
    }
    
    public override bool Equals(object? obj)
    {
        if(obj is ExternalDevice other)
            return Info.Equals(other.Info);
        return false;
    }

    public override int GetHashCode()
    {
        return Info.GetHashCode();
    }

    public void Initialize()
    {
        IsPlugged = false;
        ExternalDevicesManager.Instance.DevicesListUpdated += OnDevicesListUpdated;
        OnDevicesListUpdated(null, EventArgs.Empty);
    }

    private void OnDevicesListUpdated(object? sender, EventArgs e)
    {
        bool plugged;
        MtpDeviceInfo? info;
        lock (ExternalDevicesManager.Instance.DevicesLock)
            info = ExternalDevicesManager.Instance.Devices.FirstOrDefault(x=> x.Name.Equals(Name));

        if (info != null)
        {
            IsPlugged = true;
            Info = info;
            LastSeen = DateTime.UtcNow;    
        }
        
    }
    public void Dispose()
    {
        ExternalDevicesManager.Instance.DevicesListUpdated -= OnDevicesListUpdated;
    }

    [RelayCommand(CanExecute = nameof(CanPickDirectory))]
    async Task PickDirectory()
    {
        if(Info == null || Presenter?.Window == null ) return;

        if (await ExternalDevicesManager.MountDeviceAsync(Info))
        {
            try
            {
                var path = await DialogUtils.PickDirectoryAsync(Presenter.Window, Info.Info?.LocalPath) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(path) && ! string.IsNullOrWhiteSpace(Info.Info?.LocalPath))
                {
                    MusicPath = PathUtils.GetRelativePath(Info.Info.LocalPath, path);
                }
            }
            finally
            {
                await ExternalDevicesManager.UnmountDeviceAsync(Info);
            }
        }
        else
        {
            await DialogUtils.MessageBox(Presenter.Window, "Error", "MusicaLibre Could not mount this device, Ensure it is connected and USB Data Transfers are enabled.");
        }
            
    }

    public bool CanPickDirectory()
    {
        return Presenter?.Window != null && Info != null;
    }

    [RelayCommand(CanExecute = nameof(CanDelete))] void Delete() => Presenter?.ExternalDevices.Remove(this);
    bool CanDelete()=> Presenter != null;

}