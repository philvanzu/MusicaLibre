using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.ViewModels;

// constructor must run on the ui thread for the progress object to be valid
public partial class ProgressDialogViewModel: ProgressViewModel
{
    private readonly TaskCompletionSource _shownTcs = new TaskCompletionSource();
    public Task DialogShown => _shownTcs.Task;
    

    public ProgressDialogViewModel()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            throw(new InvalidOperationException("ProgressDialogViewModel constructor Must be invoked on the UI thread."));
        }
    }

    public async Task Show()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            throw(new InvalidOperationException("ProgressDialogViewModel.Show Must be invoked on the UI thread."));
        }
        var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (window != null)
        {
            try
            {
                await DialogUtils.ShowDialogAsync<object>(window, this);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        
    }

    public override void Close()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            var window = app.Windows.FirstOrDefault(w => w.DataContext == this);
            window?.Close(null); // Return the result from ShowDialog
        }
    }

    public void NotifyDialogShown()
    {
        _shownTcs.TrySetResult();
    }
}