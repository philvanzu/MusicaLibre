using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.Views;

namespace MusicaLibre.ViewModels;

public partial class AppSettingsViewModel:ViewModelBase
{
    AppSettingsDialog Window {get; init;}
    [ObservableProperty] ObservableCollection<ExternalDevice> _externalDevices;
    
    public AppSettingsViewModel(AppSettingsDialog window)
    {
        Window = window;
        _externalDevices = new ObservableCollection<ExternalDevice>(AppData.Instance.ExternalDevices);
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
        var dlg = new DevicePickDialog();
        var vm = new DevicePickViewModel()
        {
            Title = $"Device Picker",
            Content =$"Currently Available Devices:",
            ShowCancelButton = true,
            //List = new(ExternalDevicesManager.Instance.Devices.Values.Select(x=>x.Name).ToList()),
            SelectedIndex = -1,
        };
        dlg.DataContext = vm;
        if (await dlg.ShowDialog<bool>(Window))
        {
            var deviceName = vm.List[vm.SelectedIndex];
            //var device = ExternalDevicesManager.Instance.Devices.Values.FirstOrDefault(x=> x.Name.Equals(deviceName));
        }
    }  
    
    
}