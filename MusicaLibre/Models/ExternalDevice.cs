using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Models;


public partial class ExternalDevice : ViewModelBase , IDisposable
{
    
    public MtpDeviceInfo Info { get; }
    [ObservableProperty] private string _name;
    
    [ObservableProperty] private bool _isPlugged;
    [ObservableProperty] private string _musicPath;
    [ObservableProperty] private int _maxBitrate = 320;
    public DateTime LastSeen {get; set;}
    
    public ExternalDevice(MtpDeviceInfo info, string name, string mountPoint)
    {
        Info = info;
        Name = name;
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
        lock (ExternalDevicesManager.Instance.DevicesLock)
            plugged = ExternalDevicesManager.Instance.Devices.Contains(Info);

        IsPlugged = plugged;
        if (plugged) LastSeen = DateTime.UtcNow;
    }
    public void Dispose()
    {
        ExternalDevicesManager.Instance.DevicesListUpdated -= OnDevicesListUpdated;
    }
    
}