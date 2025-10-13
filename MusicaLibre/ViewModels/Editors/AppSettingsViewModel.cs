using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.ViewModels;
using MusicaLibre.Services;
using MusicaLibre.Views;

namespace MusicaLibre.ViewModels;

public partial class AppSettingsViewModel:ViewModelBase
{
    public AppSettingsDialog Window {get; init;}
    [ObservableProperty] ObservableCollection<ExternalDevice> _externalDevices;
    [ObservableProperty] ExternalDevice? _selectedDevice;
    
    public AppSettingsViewModel(AppSettingsDialog window)
    {
        Window = window;
        window.Closing += OnWindowClosing;
        _externalDevices = new ObservableCollection<ExternalDevice>(AppData.Instance.ExternalDevices);
        foreach (var device in _externalDevices)
        {
            device.Presenter = this;
            if (device.Info is null)
            {
                device.Info = ExternalDevicesManager.Devices.FirstOrDefault(x=> x.Name.Equals(device.Name));
                if(device.Info is not null)
                    device.IsPlugged = true;    
            }
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        foreach (var device in ExternalDevices)
        {
            device.Presenter = null;
        }
        Window.Closing -= OnWindowClosing;
    }

    [RelayCommand] private void Ok()
    {
        AppData.Instance.ExternalDevices = ExternalDevices.ToList();
        Window.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        Window.Close();
    }

    [RelayCommand]
    async Task AddDevice()
    {
        var dlg = new ListPickDialog();
        var vm = new ListPickViewModel()
        {
            Title = $"Device Picker",
            Content =$"Currently Available Devices:",
            ShowCancelButton = true,
            List = new(ExternalDevicesManager.Devices.Select(x=>x.Name).ToList()),
            SelectedIndex = -1,
        };
        dlg.DataContext = vm;
        if (await dlg.ShowDialog<bool>(Window))
        {
            var deviceName = vm.List[vm.SelectedIndex];
            var device = ExternalDevicesManager.Devices.FirstOrDefault(x=> x.Name.Equals(deviceName));
            if (device != null)
            {
                var ed = new ExternalDevice(device)
                {
                    Presenter = this,
                };
                ExternalDevices.Add(ed);
            }
        }
    }  
    
    
}