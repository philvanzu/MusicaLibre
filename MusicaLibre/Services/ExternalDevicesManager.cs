using System; 
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using SQLitePCL;
using Usb.Events;
using Task = System.Threading.Tasks.Task;


namespace MusicaLibre.Services;
//Singleton, maintains a list of currently plugged mtp devices and emits DevicesListUpdated events
public partial class ExternalDevicesManager 
{ 
    private static Dictionary<string, MtpDeviceInfo> _devices = new();

    public static List<MtpDeviceInfo> Devices
    {
        get { lock (DevicesLock) return new List<MtpDeviceInfo>(_devices.Values); }
    }

    private static int _listerRunning = 0;
    private static UsbEventWatcher? _watcher; 
     
    public static event EventHandler<EventArgs>? DevicesListUpdated;
    
    public static readonly object DevicesLock = new(); private ExternalDevicesManager() { } 
    
    public static async Task Start() { 
        if (_watcher != null) return;
        await RefreshDevicesAsync();

        _watcher = new(); 
        _watcher.UsbDeviceAdded += (sender, e) =>
        {
            _ = RefreshDevicesAsync(2000);
        }; 
        _watcher.UsbDeviceRemoved += (sender, e) =>{
            _ = RefreshDevicesAsync(2000);
        }; 
        _watcher.Start();

    }

    public static void Stop()
    {
        _watcher?.Dispose(); _watcher = null;
    }

    private static async Task RefreshDevicesAsync(int delay = 0)
    {
        if (Interlocked.Exchange(ref _listerRunning, 1) == 0)
        {
            try
            {
                await Task.Delay(delay);
                Dictionary<string, MtpDeviceInfo>? devices = null;
                devices = await ListMtpDevices();
                try
                {
                    lock (DevicesLock) _devices = devices; 
                    Dispatcher.UIThread.Post(()=>DevicesListUpdated?.Invoke(null, EventArgs.Empty));
                } catch (Exception ex){ Console.WriteLine(ex); }
            }
            finally
            {
                // Release the lock
                Interlocked.Exchange(ref _listerRunning, 0);
            }
        }
    }

    
    
    public static async Task<Dictionary<string, MtpDeviceInfo>> ListMtpDevices()
    {
        var devices = new Dictionary<string, MtpDeviceInfo>();

        var psi = new ProcessStartInfo("gio", $"mount -li")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        if(string.IsNullOrEmpty(output))return devices;

        var entities = ParseGioOutput(output);
        foreach(var entity in entities)
            ProcessGioEntity(entity, ref devices);
        return devices;
    }

    private static void ProcessGioEntity(GioEntity entity, ref Dictionary<string, MtpDeviceInfo> devices)
    {
        if (entity.IsMusicPlayer)
        {
            entity.Properties.TryGetValue("Type", out var type);
            entity.Properties.TryGetValue("activation_root", out var activationRoot);
            entity.Properties.TryGetValue("default_location", out var defaultLoc);
            var uri = activationRoot ?? defaultLoc;
            
            if (!string.IsNullOrEmpty(uri) && uri.StartsWith("mtp://"))
            {
                entity.Properties.TryGetValue("unix-device", out var id);
                devices[id ?? entity.Name] = new MtpDeviceInfo()
                {
                    Name = entity.Name,
                    Id = id ?? entity.Name,
                    Uri = uri,
                };
            }
        }
        else
        {
            foreach(var child in entity.Children)
                ProcessGioEntity(child, ref devices);
        }
    }
    
    private static List<GioEntity> ParseGioOutput(string output)
    {
        var roots = new List<GioEntity>();
        var stack = new Stack<(int indent, GioEntity entity)>();

        GioEntity? current = null;

        using var reader = new StringReader(output);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Count leading spaces for hierarchy inference
            int indent = line.TakeWhile(char.IsWhiteSpace).Count();
            string trimmed = line.Trim();

            // Start of new entity
            if (trimmed.StartsWith("Drive(") || trimmed.StartsWith("Volume(") || trimmed.StartsWith("Mount("))
            {
                var entity = new GioEntity();

                if (trimmed.StartsWith("Drive("))
                    entity.Type = "Drive";
                else if (trimmed.StartsWith("Volume("))
                    entity.Type = "Volume";
                else
                    entity.Type = "Mount";

                // Extract display name
                int colonIndex = trimmed.IndexOf(':');
                if (colonIndex >= 0)
                    entity.Name = trimmed[(colonIndex + 1)..].Split("->")[0].Trim();

                // Adjust stack: pop until parent has smaller indent
                while (stack.Count > 0 && stack.Peek().indent >= indent)
                    stack.Pop();

                if (stack.Count == 0)
                    roots.Add(entity);
                else
                    stack.Peek().entity.Children.Add(entity);

                stack.Push((indent, entity));
                current = entity;
                continue;
            }

            // Property line
            if (current != null)
            {
                int sepIndex = trimmed.IndexOf('=');
                if (sepIndex == -1)
                    sepIndex = trimmed.IndexOf(':');
                if (sepIndex > 0)
                {
                    string key = trimmed[..sepIndex].Trim();
                    string value = trimmed[(sepIndex + 1)..].Trim().Trim('\'');
                    current.Properties[key] = value;
                }
            }
        }

        return roots;
    }
    private class GioEntity
    {
        public string Type { get; set; } = ""; // Drive, Volume, Mount
        public string Name { get; set; } = "";
        public Dictionary<string, string> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<GioEntity> Children { get; set; } = new();
        public override string ToString() => $"{Type}: {Name}";
        
        public bool IsMusicPlayer
        {
            get
            {
                if (Type == "Volume")
                {
                    if (Properties.TryGetValue("Type", out var type) &&
                        (type.Contains("Mtp", StringComparison.OrdinalIgnoreCase) ||
                         type.Contains("GPhoto", StringComparison.OrdinalIgnoreCase)))
                        return true;

                    if (Properties.TryGetValue("activation_root", out var root) &&
                        root.StartsWith("mtp://", StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (Properties.TryGetValue("content_type", out var contentType) &&
                        contentType.Contains("audio", StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                if (Type == "Mount")
                {
                    if (Properties.TryGetValue("Type", out var type) &&
                        (type.Contains("Mtp", StringComparison.OrdinalIgnoreCase) ||
                         type.Contains("GPhoto", StringComparison.OrdinalIgnoreCase)))
                        return true;

                    if (Properties.TryGetValue("default_location", out var loc) &&
                        loc.StartsWith("mtp://", StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
            
        }

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
    
    public class GioHelper
   {
       private static Thread? _glibThread;
   private static IntPtr _mainLoop;
   private static IntPtr _monitor;
   private static ulong _addedHandler;
   private static ulong _removedHandler;
       public static void Start()
       {
           if (_glibThread != null)
               return;
   
           _glibThread = new Thread(() =>
           {
               _monitor = g_volume_monitor_get();
               g_object_ref(_monitor); // hold reference while running
   
               lock (DevicesLock) _devices = ListMtpDevices();
               Dispatcher.UIThread.Post(()=>DevicesListUpdated?.Invoke(null, EventArgs.Empty));
               
               // connect native signals
               _addedHandler = g_signal_connect_data(_monitor, "volume-added", OnVolumeAdded, IntPtr.Zero, IntPtr.Zero, 0);
               _removedHandler = g_signal_connect_data(_monitor, "volume-removed", OnVolumeRemoved, IntPtr.Zero, IntPtr.Zero, 0);
   
               _mainLoop = g_main_loop_new(IntPtr.Zero, false);
               g_main_loop_run(_mainLoop);
   
               // cleanup after quit
               g_signal_handler_disconnect(_monitor, _addedHandler);
               g_signal_handler_disconnect(_monitor, _removedHandler);
               g_object_unref(_monitor);
           })
           {
               IsBackground = true,
               Name = "GLibMainLoopThread"
           };
   
           _glibThread.Start();
       }
   
       public static void Stop()
       {
           if (_mainLoop != IntPtr.Zero)
           {
               g_main_loop_quit(_mainLoop);
               _mainLoop = IntPtr.Zero;
           }
           _glibThread = null;
       }
   
       [MonoPInvokeCallback(typeof(SignalCallback))]
       private static void OnVolumeAdded(IntPtr instance, IntPtr volumePtr, IntPtr userData)
       {
           if (volumePtr == IntPtr.Zero)
               return;
   
           try
           {
               string name = PtrToString(g_volume_get_name(volumePtr))!;
               IntPtr root = g_volume_get_activation_root(volumePtr);
               if (root == IntPtr.Zero)
                   return;
   
               string uri = PtrToString(g_file_get_uri(root))!;
               g_object_unref(root);
   
               if (!uri.StartsWith("mtp://"))
                   return;
   
               string id = PtrToString(g_volume_get_identifier(volumePtr, "unix-device")) ?? name;
               
               bool changed = false;
               lock (DevicesLock)
               {
                   if (!_devices.ContainsKey(id))
                   {
                       _devices[id] = new MtpDeviceInfo
                       {
                           Name = name,
                           Id = id,
                           Uri = uri
                       };
                       changed = true;
                   }
               }
               if (changed)
                   Dispatcher.UIThread.Post(()=>DevicesListUpdated?.Invoke(null, EventArgs.Empty));
           }
           catch (Exception ex)
           {
               Console.WriteLine($"OnVolumeAdded exception: {ex}");
           }
       }
   
       [MonoPInvokeCallback(typeof(SignalCallback))]
       private static void OnVolumeRemoved(IntPtr instance, IntPtr volumePtr, IntPtr userData)
       {
           if (volumePtr == IntPtr.Zero)
               return;
   
           try
           {
               string name = PtrToString(g_volume_get_name(volumePtr))!;
               IntPtr root = g_volume_get_activation_root(volumePtr);
               if (root == IntPtr.Zero)
                   return;
   
               string uri = PtrToString(g_file_get_uri(root))!;
               g_object_unref(root);
   
               if (!uri.StartsWith("mtp://"))
                   return;
   
               string id = PtrToString(g_volume_get_identifier(volumePtr, "unix-device")) ?? name;
               bool changed = false;
               lock (DevicesLock) 
                   if (_devices.Remove(id)) 
                       changed = true;
               
               if (changed)
                   Dispatcher.UIThread.Post(()=>DevicesListUpdated?.Invoke(null, EventArgs.Empty));
           }
           catch (Exception ex)
           {
               Console.WriteLine($"OnVolumeRemoved exception: {ex}");
           }
       }
   
       private static string PtrToString(IntPtr ptr) => ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr)!;
       public static Dictionary<string, MtpDeviceInfo> ListMtpDevices()
   {
       var devices = new Dictionary<string, MtpDeviceInfo>();

       IntPtr volumeList = g_volume_monitor_get_volumes(_monitor);

       // Iterate the GList safely
       for (IntPtr node = volumeList; node != IntPtr.Zero; node = Marshal.ReadIntPtr(node, IntPtr.Size))
       {
           IntPtr volumePtr = Marshal.ReadIntPtr(node);
           if (volumePtr == IntPtr.Zero) continue;

           string name = PtrToString(g_volume_get_name(volumePtr))!;
           IntPtr idPtr = g_volume_get_identifier(volumePtr, "unix-device");
           string id = idPtr != IntPtr.Zero ? PtrToString(idPtr)! : name;

           // Skip invalid /dev nodes
           //if (id.StartsWith("/dev/") && !System.IO.File.Exists(id)) continue;

           IntPtr rootPtr = g_volume_get_activation_root(volumePtr);
           if (rootPtr == IntPtr.Zero) continue;

           try
           {
               string uri = PtrToString(g_file_get_uri(rootPtr))!;
               if (!uri.StartsWith("mtp://")) continue;

               var info = new MtpDeviceInfo
               {
                   Name = name,
                   Id = id,
                   Uri = uri
               };
               devices[id] = info;
           }
           finally
           {
               g_object_unref(rootPtr); // always unref once
           }
       }

       return devices;
   }
   [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_volume_monitor_get();
   [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_volume_monitor_get_volumes(IntPtr monitor);
   [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_volume_get_name(IntPtr volume); 
   [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_volume_get_activation_root(IntPtr volume);
   [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_volume_get_identifier(IntPtr volume, string kind);
   [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_file_get_uri(IntPtr file);
   [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_file_new_for_uri(string uri);
   [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_file_query_info( IntPtr file, string attributes, uint flags, IntPtr cancellable, out IntPtr error);
   [DllImport("libgobject-2.0.so.0")] private static extern IntPtr g_object_ref(IntPtr obj);
   [DllImport("libgio-2.0.so.0")] private static extern void g_object_unref(IntPtr obj);
   [DllImport("libgio-2.0.so.0")] private static extern void g_list_free_full(IntPtr list, IntPtr free_func);
   [DllImport("libglib-2.0.so.0")] private static extern IntPtr g_file_info_get_attribute_string(IntPtr info, string attribute);
   [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_file_info_get_name(IntPtr info); 
   [DllImport("libgio-2.0.so.0")] private static extern IntPtr g_mount_get_root(IntPtr file); 
   [DllImport("libglib-2.0.so.0")] private static extern IntPtr g_main_loop_new(IntPtr context, bool isRunning);
   [DllImport("libglib-2.0.so.0")] private static extern void g_main_loop_run(IntPtr loop);
   [DllImport("libglib-2.0.so.0")] private static extern void g_main_loop_quit(IntPtr loop);
   [DllImport("libgobject-2.0.so.0")] private static extern ulong g_signal_connect_data( IntPtr instance, string detailed_signal, SignalCallback handler, IntPtr data, IntPtr destroy_data, int connect_flags);
   [DllImport("libgobject-2.0.so.0")] private static extern void g_signal_handler_disconnect(IntPtr instance, ulong handlerId);
   
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
   private delegate void SignalCallback(IntPtr instance, IntPtr volume, IntPtr userData);
   } 
  
 */

