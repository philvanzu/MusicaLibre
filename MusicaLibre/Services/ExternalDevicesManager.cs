using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Usb.Events;
using GLib;
using Gio;

namespace MusicaLibre.Services;

//Singleton, maintains a list of currently plugged mtp devices and emits DevicesListUpdated events
public partial class ExternalDevicesManager : IDisposable
{

    private List<MtpDeviceInfo> _devices = new();

    public List<MtpDeviceInfo> Devices
    {
        get
        {
            lock (DevicesLock) return new List<MtpDeviceInfo>(_devices);
        }
        
    }
   
    public static ExternalDevicesManager Instance { get; } = new ExternalDevicesManager();
    UsbEventWatcher? _watcher;
    public event EventHandler? DevicesListUpdated;
    public readonly object DevicesLock = new();

    private ExternalDevicesManager() { }
    public void Start()
    {

        if (_watcher != null) return;
        Task.Run(async () =>
        {
            var devices = await LibMtp.ListDevicesAsync();
            lock (DevicesLock)
            {
                try
                {
                    _devices = devices;    
                }
                catch (Exception ex){Console.WriteLine(ex);}
                DevicesListUpdated?.Invoke(this, EventArgs.Empty);
            }
            
        });
        
        /*
        _watcher = new();
        _watcher.UsbDeviceAdded += (sender, e) =>
        {
            Task.Run(async () =>
            {
                try
                {
                    var newDevices = await GetAllMtpDevicesAsync();
                    lock (DevicesLock) _devices = newDevices.ToDictionary(x => x.Info, x => x);
                    DevicesListUpdated?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex){ Console.WriteLine(ex);}
            });
        };

        _watcher.UsbDeviceRemoved += (sender, e) =>
        {
            var key = new DeviceInfo(e.VendorID, e.ProductID, string.IsNullOrEmpty(e.SerialNumber) ? "N/A" : e.SerialNumber);
            bool updated = false;
            lock (DevicesLock)
            {
                if(_devices.Remove(key))
                        updated = true;    
                
                else if(string.IsNullOrEmpty(e.SerialNumber))
                {
                    var match = Devices.Keys.FirstOrDefault(x => x.Product.Equals(e.ProductID) && x.Vendor.Equals(e.VendorID));
                    if( match != null )
                        _devices.Remove(match);
                    updated = true;
                }
            }
            if (updated)
                DevicesListUpdated?.Invoke(this, EventArgs.Empty);
        };

        _watcher.Start(); 
        */ 
    }

    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
    }


    private static async Task<List<MtpDeviceInfo>> GetDevices()
    {
        var devices = new List<MtpDeviceInfo>();
        
        return devices;
    }

    private static async Task<bool> BusAndDevMatchAsync(MtpDeviceInfo device, string gvfsBus, string gvfsDev)
    {
        int targetBus = int.Parse(gvfsBus);
        int targetDev = int.Parse(gvfsDev);

        string sysUsbPath = "/sys/bus/usb/devices";

        foreach (var devDir in Directory.EnumerateDirectories(sysUsbPath))
        {
            string? vendor = await TryReadFileAsync(Path.Combine(devDir, "idVendor"));
            string? product = await TryReadFileAsync(Path.Combine(devDir, "idProduct"));
            string? serial = await TryReadFileAsync(Path.Combine(devDir, "serial"));

            if (vendor != device.Vendor || product != device.Product || (device.Serial != "" && serial != device.Serial))
                continue;

            string? busnumStr = await TryReadFileAsync(Path.Combine(devDir, "busnum"));
            string? devnumStr = await TryReadFileAsync(Path.Combine(devDir, "devnum"));

            if (busnumStr != null && devnumStr != null &&
                int.TryParse(busnumStr, out int busnum) &&
                int.TryParse(devnumStr, out int devnum))
            {
                if (busnum == targetBus && devnum == targetDev)
                    return true;
            }
        }

        return false;
    }

    private static async Task<string?> TryReadFileAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            return (await File.ReadAllTextAsync(path)).Trim();
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}

public class MtpDeviceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty; // stable across sessions
    public List<MtpVolume> Volumes { get; set; } = new();
}
public class MtpVolume
{
    public string Name { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public Gio.File Root { get; set; } = null!;
}





