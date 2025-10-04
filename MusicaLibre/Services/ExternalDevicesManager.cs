using System; 
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Usb.Events; 
using Gio;

using System.Threading;
using GObject;
using Mount = Gio.Internal.Mount;
using Task = System.Threading.Tasks.Task;
using Type = System.Type;

namespace MusicaLibre.Services;
//Singleton, maintains a list of currently plugged mtp devices and emits DevicesListUpdated events
public partial class ExternalDevicesManager : IDisposable 
{ 
    private List<MtpDeviceInfo> _devices = new();

    public List<MtpDeviceInfo> Devices
    {
        get { lock (DevicesLock) return new List<MtpDeviceInfo>(_devices); }
    } 
    public static ExternalDevicesManager Instance { get; } = new ExternalDevicesManager(); 
    UsbEventWatcher? _watcher; public event EventHandler? DevicesListUpdated; 
    public readonly object DevicesLock = new(); private ExternalDevicesManager() { } 
    public void Start() { 
        if (_watcher != null) return; 
        Task.Run(async () =>
        {
            var devices = await ListMtpDevicesAsync();
            lock (DevicesLock)
            {
                try { _devices = devices; } catch (Exception ex){Console.WriteLine(ex);} DevicesListUpdated?.Invoke(this, EventArgs.Empty);
            }
        }); 
/*
         _watcher = new(); 
         _watcher.UsbDeviceAdded += (sender, e) => { 
             Task.Run(async () => 
             { 
                 try 
                 { 
                     var newDevices = await GetAllMtpDevicesAsync(); 
                     lock (DevicesLock) _devices = newDevices.ToDictionary(x => x.Info, x => x); DevicesListUpdated?.Invoke(this, EventArgs.Empty); 
                 } catch (Exception ex){ Console.WriteLine(ex); } 
             });
          }; 
          _watcher.UsbDeviceRemoved += (sender, e) =>{ 
            var key = new DeviceInfo(e.VendorID, e.ProductID, string.IsNullOrEmpty(e.SerialNumber) ? "N/A" : e.SerialNumber); 
            bool updated = false; lock (DevicesLock) 
            { 
                if(_devices.Remove(key)) 
                    updated = true; 
                else if(string.IsNullOrEmpty(e.SerialNumber)) 
                { 
                    var match = Devices.Keys.FirstOrDefault(x => x.Product.Equals(e.ProductID) && x.Vendor.Equals(e.VendorID));
                    if( match != null ) _devices.Remove(match); 
                    updated = true; 
                } 
            } 
            if (updated) DevicesListUpdated?.Invoke(this, EventArgs.Empty); 
        }; 
        _watcher.Start();
*/ 
    }

    public void Stop()
    {
        _watcher?.Dispose(); _watcher = null;
    }

    private static async Task<List<MtpDeviceInfo>?> ListMtpDevicesAsync()
    {
        List<MtpDeviceInfo>? devices = null; 
        await Task.Run(() =>
        {
            devices = ListMtpDevices();
        }); 
        return devices;
    } 
    [DllImport("Gio")] private static extern IntPtr g_volume_get_name(IntPtr volume); 
    [DllImport("Gio")] private static extern IntPtr g_volume_get_activation_root(IntPtr volume);
    [DllImport("Gio")] private static extern IntPtr g_volume_get_identifier(IntPtr volume, string kind);
    [DllImport("Gio")] private static extern IntPtr g_file_get_uri(IntPtr file); 
    [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_file_enumerate_children( IntPtr file, string attributes, IntPtr cancellable, out IntPtr error); 
    [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_file_enumerator_next_file( IntPtr enumerator, IntPtr cancellable, out IntPtr error); 
    [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_file_info_get_name(IntPtr info); 
    [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_file_new_for_uri(string uri); 
    [DllImport("Gio")] private static extern IntPtr g_mount_get_root(IntPtr file); 
     
    [DllImport("Gio")] private static extern void g_object_unref(IntPtr obj); 
    
    private static string PtrToString(IntPtr ptr) => ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr)!;
    public static List<MtpDeviceInfo> ListMtpDevices() 
    { 
        var devices = new Dictionary<string, MtpDeviceInfo>(); 
        var monitor = VolumeMonitor.Get(); 
        var volumes = monitor.GetVolumes();

        // GLib.List of GVolume*
        GLib.List.Foreach(volumes, data =>
        {

            var volumePtr = (IntPtr)data; 
            if (volumePtr == IntPtr.Zero) 
                return; 
            
            var name = PtrToString(g_volume_get_name(volumePtr)); 
            var idPtr = g_volume_get_identifier(volumePtr, "unix-device"); 
            string id = idPtr != IntPtr.Zero ? PtrToString(idPtr)! : name; 
            var rootPtr = g_volume_get_activation_root(volumePtr); 
            if (rootPtr == IntPtr.Zero) return; 
            var uriPtr = g_file_get_uri(rootPtr); 
            var uri = PtrToString(uriPtr); if (!uri.StartsWith("mtp://")) return;
            if (!devices.TryGetValue(id, out var device))
            {
                device = new MtpDeviceInfo
                {
                    Name = name, 
                    Id = id, 
                    Uri = uri,
                }; 
                devices[id] = device;
            } 
        }); 
        
        return new List<MtpDeviceInfo>(devices.Values); 
    } 
    

    private static async Task<string?> TryReadFileAsync(string path)
    {
        try
        {
            if (!System.IO.File.Exists(path)) return null; return (await System.IO.File.ReadAllTextAsync(path)).Trim();
        } catch { return null; }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    } 
} 
public class MtpDeviceInfo 
{ 
    public string Name { get; set; } = string.Empty; 
    public string Id { get; set; } = string.Empty; 
    // stable across sessions
    public string Uri { get; set; } = string.Empty; 
}

