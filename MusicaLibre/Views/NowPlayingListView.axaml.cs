using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class NowPlayingListView : UserControl
{
    public NowPlayingListView()
    {
        InitializeComponent();
    }

    private void TrackItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Handled) return;
        if (sender is Border border && border.DataContext is TrackViewModel track)
        {
            if(InputManager.CtrlPressed) track.IsSelected = !track.IsSelected;
            else
            {
                if (track.IsSelected && !InputManager.ShiftPressed) track.Presenter.SelectedItem = null;
                track.IsSelected = true;    
            }
        }
    }

    private void TrackItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Handled) return;
        if (sender is Border border && border.DataContext is TrackViewModel track)
        {
            if(!track.IsPlaying)
                track.IsPlaying = true;
            else track.Presenter.Library.MainWindowViewModel.Player.PlayToggleCommand.Execute(null);
        }
    }
}