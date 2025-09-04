using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
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
            track.IsPlaying = true;
            
        }
    }
}