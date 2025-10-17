using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using MusicaLibre.Models;
using MusicaLibre.Views; 
using MusicaLibre.ViewModels;
namespace MusicaLibre.Services;

public static class DialogUtils
{
    public static async Task<string?> PickFileAsync(Window owner, string directoryPath, IEnumerable<FilePickerFileType>? filters = null)
    {
        var options = new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select a file"
        };
        if (!string.IsNullOrEmpty(directoryPath))
            options.SuggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(directoryPath);

        if (filters != null)
            options.FileTypeFilter = filters.ToList();

        var files = await owner.StorageProvider.OpenFilePickerAsync(options);

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
    public static async Task<string?> SaveFileAsync(
        Window owner,
        string directoryPath,
        IEnumerable<FilePickerFileType>? filters = null,
        string? suggestedFileName = null)
    {
        var options = new FilePickerSaveOptions
        {
            Title = "Save file",
            SuggestedFileName = suggestedFileName ?? "NewFile"
        };

        if (!string.IsNullOrEmpty(directoryPath))
            options.SuggestedStartLocation = 
                await owner.StorageProvider.TryGetFolderFromPathAsync(directoryPath);

        if (filters != null)
            options.FileTypeChoices = filters.ToList();

        var file = await owner.StorageProvider.SaveFilePickerAsync(options);

        return file?.Path.LocalPath;
    }
    public static async Task<string?> PickDirectoryAsync(Window owner, string? directoryPath=null)
    {
        IStorageFolder? startFolder = null;
        
        if (!string.IsNullOrWhiteSpace(directoryPath))
            startFolder = await owner.StorageProvider.TryGetFolderFromPathAsync(directoryPath);
        
        var options = new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select a folder",
            SuggestedStartLocation = startFolder
        };
        
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(options);

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    public static async Task<TResult?> ShowDialogAsync<TResult>(Window owner, object viewModel)
    {
        var window = CreateWindowForViewModel(viewModel);
        window.DataContext = viewModel;
        return await window.ShowDialog<TResult>(owner);
    }
    
    

    public static void ShowModelessDialog(Window owner, object viewModel)
    {
        var window = CreateWindowForViewModel(viewModel);
        window.DataContext = viewModel;
        window.Show(owner);
    }
    public static Window CreateWindowForViewModel(object viewModel)
    {
        return viewModel switch
        {
            OkCancelViewModel vm => new OkCancelDialog(vm), 
            ProgressDialogViewModel vm => new ProgressDialog(vm),
            _ => throw new NotImplementedException($"No view mapped for view model: {viewModel.GetType().Name}")
        };
    }
    
    public static async Task<Artwork?> PickArtwork(Window owner, LibraryViewModel library, string rootDirectory, ArtworkRole role)
    {
        var selectedPath = await DialogUtils.PickFileAsync(owner, rootDirectory,new[]
        {
            new FilePickerFileType("Image Files") { Patterns = new[] { "*.jpg",  "*.jpeg", "*.png",  "*.bmp",  "*.gif", "*.webp" } },
            FilePickerFileTypes.All // built-in “All files (*.*)”
        } );
        if (!string.IsNullOrEmpty(selectedPath)  && File.Exists(selectedPath))
        {
            var defaultFolder = library.Data.Folders.Values.FirstOrDefault(x=> x.Name.Equals(rootDirectory));
            return await Artwork.InsertIfNotExist(library, selectedPath, role, ArtworkSourceType.External, defaultFolder);
        }
        return null;
    }

    public static async Task MessageBox(Window owner, string title, string message)
    {
        var dlg = new OkCancelDialog();
        var vm = new OkCancelViewModel()
        {
            Title = title,
            Content = message,
            ShowCancelButton = false,
        };
        dlg.DataContext = vm;
        await dlg.ShowDialog(owner);
    }
    
    public static async Task<bool> YesNoDialog(Window owner, string title, string message)
    {
        var dlg = new OkCancelDialog();
        var vm = new OkCancelViewModel()
        {
            Title = title,
            Content = message,
            ShowCancelButton = true,
            OkText = "Yes",
            CancelText = "No"
        };
        dlg.DataContext = vm;
        return await dlg.ShowDialog<bool>(owner);
    }

}