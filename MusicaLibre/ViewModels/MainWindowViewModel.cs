using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;
using MusicaLibre.Views;

namespace MusicaLibre.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private DialogService DialogService { get; init; }
    [ObservableProperty] ProgressViewModel _statusProgress =  new ProgressViewModel();
    [ObservableProperty] LibraryViewModel? _library;
    
    [ObservableProperty] PlayerViewModel _player;
    
    private ProgressDialogViewModel _progressDialog;
    public Window MainWindow { get; init; }
  
    public MainWindowViewModel(Window mainWindow, DialogService dialogService)
    {
        MainWindow = mainWindow;
        DialogService = dialogService;
        _progressDialog = new ProgressDialogViewModel(DialogService);
        
        _player = new PlayerViewModel(this);
    }
    public void OnWindowOpened()
    {
        var path = AppData.Instance.AppState.CurrentLibrary;
        if (!string.IsNullOrEmpty(path))
        {
            var info = new FileInfo(path);
            Library = LibraryViewModel.Load(info, this);
            if (Library != null)
            {
                Library.Open();
            }    
        }
        
    }
    public void OnWindowClosing()
    {
        if (Library != null)
        {
            Library.Close();
        }
    }
    [RelayCommand]
    private async Task CreateLibraryAsync()
    {
        await  Dispatcher.UIThread.InvokeAsync(async () =>
        {
            string? selectedPath;
            selectedPath = await DialogService.PickDirectoryAsync(MainWindow);

            if (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath))
            {
                try
                {
                    selectedPath = selectedPath.TrimEnd(System.IO.Path.DirectorySeparatorChar,
                        System.IO.Path.AltDirectorySeparatorChar);
                    selectedPath += System.IO.Path.DirectorySeparatorChar;

                    DirectoryInfo info = new DirectoryInfo(selectedPath);
                    var library = LibraryViewModel.Create(info, this);
                    if (library == null) return;
                    Dispatcher.UIThread.Post(() => _ = _progressDialog.Show(), DispatcherPriority.Render);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _progressDialog.DialogShown;
                            await Task.Delay(1);
                            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                            await library.CreateLibrary(info.FullName, _progressDialog);

                            Library = library;
                            Library.Open();
                        }
                        catch (Exception ex){Console.WriteLine(ex);}
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }    
        });

    }
    
    [RelayCommand]
    private async Task OpenLibraryAsync()
    {
        await  Dispatcher.UIThread.InvokeAsync(async () =>
        {
            string? selectedPath;
            selectedPath = await DialogService.PickFileAsync(MainWindow, AppData.Path, new[]
            {
                new FilePickerFileType("MusicaLibre Library Files") { Patterns = new[] { "*.db" } },
                FilePickerFileTypes.All // built-in “All files (*.*)”
            });

            if (!string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath))
            {
                var info = new FileInfo(selectedPath);
                Library = LibraryViewModel.Load(info, this);
                if (Library != null)
                {
                    Library.Open();
                }
                    
            }    
        });

    }


}