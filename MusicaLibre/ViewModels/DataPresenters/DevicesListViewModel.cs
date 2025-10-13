using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public partial class DevicesListViewModel:ViewModelBase
{
    public static DevicesListViewModel Instance { get; } = new DevicesListViewModel();
    private static readonly ObservableCollection<ExternalDevice> _externalDevicesMutable 
        = new ObservableCollection<ExternalDevice>();

    public static ReadOnlyObservableCollection<ExternalDevice> ExternalDevices { get; private set; }= new ReadOnlyObservableCollection<ExternalDevice>(_externalDevicesMutable);
    [ObservableProperty] private bool _showDevicesMenu;

    private DevicesListViewModel()
    {
        ExternalDevicesManager.DevicesListUpdated += OnDevicesListUpdated;
        
        
    }
    private void OnDevicesListUpdated(object? sender, EventArgs e)
    {
        _externalDevicesMutable.Clear();
        var plugged = new List<ExternalDevice>();
        foreach (var device in AppData.Instance.ExternalDevices)
        {
            device.IsPlugged = false;
            device.Info = null;
            if (ExternalDevicesManager.Devices is not null && ExternalDevicesManager.Devices.Count > 0)
            {
                device.Info = ExternalDevicesManager.Devices.FirstOrDefault(x=> x.Name.Equals(device.Name));
                if (device.Info != null)
                {
                    device.IsPlugged = true;
                    plugged.Add(device);
                }    
            }
        };
        _externalDevicesMutable.AddRange(plugged);
        ShowDevicesMenu = plugged.Count > 0;
    }

    public void RefreshSyncedDevicesList()
    {
        OnDevicesListUpdated(null, EventArgs.Empty);
    }
}