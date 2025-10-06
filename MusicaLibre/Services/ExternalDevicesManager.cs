using System; 
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Usb.Events; 
using Gio;
using Task = System.Threading.Tasks.Task;


namespace MusicaLibre.Services;
//Singleton, maintains a list of currently plugged mtp devices and emits DevicesListUpdated events
public partial class ExternalDevicesManager : IDisposable 
{ 
    private List<MtpDeviceInfo> _devices = new();

    public List<MtpDeviceInfo> Devices
    {
        get { lock (DevicesLock) return new List<MtpDeviceInfo>(_devices); }
    }

    
    
    private int _listerRunning = 0;
    
    public static ExternalDevicesManager Instance { get; } = new ExternalDevicesManager(); 
    UsbEventWatcher? _watcher; public event EventHandler? DevicesListUpdated; 
    public readonly object DevicesLock = new(); private ExternalDevicesManager() { } 
    public async Task Start() { 
        if (_watcher != null) return;
        await RefreshDevicesAsync();

         _watcher = new(); 
         _watcher.UsbDeviceAdded += (sender, e) =>
         {
             _ = RefreshDevicesAsync();
         }; 
          _watcher.UsbDeviceRemoved += (sender, e) =>{
              _ = RefreshDevicesAsync();
        }; 
        _watcher.Start();

    }

    public void Stop()
    {
        _watcher?.Dispose(); _watcher = null;
    }

    private async Task RefreshDevicesAsync()
    {
        if (Interlocked.Exchange(ref _listerRunning, 1) == 0)
        {
            try
            {
                await Task.Run( () => 
                {
                    List<MtpDeviceInfo>? devices = null;
                    devices = ListMtpDevices();
                    try
                    {
                        lock (DevicesLock)
                        {
                            _devices = (devices == null) ? new List<MtpDeviceInfo>(): devices; 
                            DevicesListUpdated?.Invoke(this, EventArgs.Empty);
                        }
              
                    } catch (Exception ex){ Console.WriteLine(ex); }
                });
            }
            finally
            {
                // Release the lock
                Interlocked.Exchange(ref _listerRunning, 0);
            }
        }
    }
    
    [DllImport("Gio")] private static extern IntPtr g_volume_get_name(IntPtr volume); 
    [DllImport("Gio")] private static extern IntPtr g_volume_get_activation_root(IntPtr volume);
    [DllImport("Gio")] private static extern IntPtr g_volume_get_identifier(IntPtr volume, string kind);
    [DllImport("Gio")] private static extern IntPtr g_file_get_uri(IntPtr file);
    [DllImport("Gio")] private static extern IntPtr g_file_new_for_uri(string uri);
    [DllImport("Gio")] private static extern IntPtr g_file_query_info( IntPtr file, string attributes, uint flags, IntPtr cancellable, out IntPtr error);
    [DllImport("Gio")] private static extern void g_object_unref(IntPtr obj);
    
    [DllImport("Glib")] private static extern IntPtr g_file_info_get_attribute_string(IntPtr info, string attribute);
    [DllImport("Gio")] private static extern IntPtr g_file_info_get_name(IntPtr info); 
    [DllImport("Gio")] private static extern IntPtr g_mount_get_root(IntPtr file); 
     
 
    
    private static string PtrToString(IntPtr ptr) => ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr)!;
    public static List<MtpDeviceInfo> ListMtpDevices() 
    { 
        var devices = new Dictionary<string, MtpDeviceInfo>(); 
        using var monitor = VolumeMonitor.Get(); 
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
            g_object_unref(rootPtr);
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

    private static async Task<UriInfo?> GetUriInfoAsync(string uri)
    {
        var psi = new ProcessStartInfo("gio", $"info \"{uri}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        
        if(string.IsNullOrEmpty(output))return null;
        string? type = Regex.Match(output, @"\bstandard::type:\s*(.+)", RegexOptions.Multiline).Groups[1].Value.Trim();
        return new UriInfo()
        {
            LocalPath = Regex.Match(output, @"\blocal path:\s*(.+)", RegexOptions.Multiline).Groups[1].Value.Trim(),
            UnixMountPath = Regex.Match(output, @"\bunix mount:\s*(.+)", RegexOptions.Multiline).Groups[1].Value.Trim(),
            FsType = Regex.Match(output, @"\bfilesystem::type:\s*(.+)", RegexOptions.Multiline).Groups[1].Value.Trim(),
            StandardType = type switch
            {
                "0" => "Unknown",
                "1" => "Regular File",
                "2" => "Directory",
                "3" => "Symbolic Link",
                "4" => "Special File",
                "5" => "Shortcut",
                "6" => "Mountable",
                _ => "Unknown"
            },
            StandardContentType = Regex.Match(output, @"\bstandard::content-type:\s*(.+)", RegexOptions.Multiline).Groups[1].Value.Trim(),
            CanRead = Regex.IsMatch(output, @"access::can-read:\s*TRUE", RegexOptions.IgnoreCase),
            CanWrite = Regex.IsMatch(output, @"access::can-write:\s*TRUE", RegexOptions.IgnoreCase)
        };
    }

    public static async Task<bool> MountDeviceAsync(MtpDeviceInfo device)
    {
        try
        {
            var envType = LinuxUtils.AssessEnvironment();
            switch (envType)
            {
                case LinuxUtils.EnvironmentType.KDE:
                    await KillKioDaemon(device.Uri);
                    await GioMountAsync(device.Uri);
                    device.Info = await GetUriInfoAsync(device.Uri);
                    break;
                default: return false;
            }
            
            if (string.IsNullOrWhiteSpace(device.Info?.LocalPath))
                return false;

            return true;      
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }
    }

    static async Task GioMountAsync(string uri)
    {
        // Step 2: Mount the device
        var mountPsi = new ProcessStartInfo("gio", $"mount \"{uri}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var mountProcess = Process.Start(mountPsi)!)
        {
            await mountProcess.WaitForExitAsync();
        }
    }
    static async Task KillKioDaemon(string uri)
    {
        try
        {
            var killPsi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments =
                    $"-c \"killall kiod6 gvfsd-mtp gvfsd-fuse gvfsd 2>/dev/null; fusermount -u /run/user/1000/gvfs/{uri} 2>/dev/null\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var killProcess = Process.Start(killPsi)!)
            {
                await killProcess.WaitForExitAsync();
            }
        }
        catch (Exception ex) { Console.WriteLine(ex); }
    }    
    public static async Task<bool> UnmountDeviceAsync(MtpDeviceInfo device)
    {
        if (device == null) 
            throw new ArgumentNullException(nameof(device));

        if (string.IsNullOrWhiteSpace(device.Uri) || device.Info != null)
            return false;

        try
        {
            var psi = new ProcessStartInfo("gio", $"mount -u \"{device.Uri}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)!;
            await process.WaitForExitAsync();

            // Update device state
            device.Info = null;
            return true;
        }
        catch
        {
            return false;
        }
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
    public UriInfo? Info { get; set; } = null!;
}

public class UriInfo
{
    public string? LocalPath { get; set; }
    public string? UnixMountPath { get; set; }
    public string StandardType {get; set; } = string.Empty;
    public string StandardContentType {get; set; } = string.Empty;
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public string FsType { get; set; } = string.Empty;
}

//Gio info example output
/*
   ❯ gio info mtp://Xiaomi_POCO_X3_Pro_5c7f3838/
   display name:
   name: Xiaomi_POCO_X3_Pro_5c7f3838
   type: directory
   size:  0
   uri: mtp://Xiaomi_POCO_X3_Pro_5c7f3838/
   local path: /run/user/1000/gvfs/mtp:host=Xiaomi_POCO_X3_Pro_5c7f3838
   unix mount: gvfsd-fuse /run/user/1000/gvfs fuse.gvfsd-fuse rw,nosuid,nodev,relatime,user_id=1000,group_id=1000
   attributes:
     standard::type: 2
     standard::name: Xiaomi_POCO_X3_Pro_5c7f3838
     standard::display-name:
     standard::icon: phone, phone-symbolic
     standard::content-type: inode/directory
     standard::size: 0
     standard::symbolic-icon: phone-symbolic, phone
     id::filesystem: mtp:host=Xiaomi_POCO_X3_Pro_5c7f3838
     access::can-read: TRUE
     access::can-write: FALSE
     access::can-execute: TRUE
     access::can-delete: FALSE
     access::can-trash: FALSE
     access::can-rename: FALSE
     filesystem::type: mtpfs
     filesystem::remote: FALSE
 */