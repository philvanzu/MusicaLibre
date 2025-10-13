using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class AlbumsListView : UserControl
{
    private AlbumsListViewModel? _oldvm;
    public AlbumsListView()
    {
        InitializeComponent();

    }

 

    private void AlbumItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Handled) return;
        if (sender is Border border && border.DataContext is AlbumViewModel album)
        {
            if (album.IsSelected && InputManager.CtrlPressed) album.IsSelected = false;
            else
            {
                if (album.IsSelected && !InputManager.ShiftPressed) album.Presenter.SelectedItem = null;
                album.IsSelected = true;
            }
        }
    }
    private void PlayButton_Click(object? sender, RoutedEventArgs e)
    {
        // Execute your play logic
        if (sender is Button button && button.DataContext is AlbumViewModel album)
        {
            album.PlayCommand.Execute(null);
            e.Handled = true;
        }

        // Prevent parent handlers (like Border.Tapped) from firing
        e.Handled = true;
    }
    private void InsertButton_Click(object? sender, TappedEventArgs e)
    {
        if (sender is Button button && button.DataContext is AlbumViewModel vm)
        {
            vm.InsertNextCommand.Execute(null);
        }
        e.Handled = true;
    }

    private void AppendButton_Click(object? sender, TappedEventArgs e)
    {
        if (sender is Button button && button.DataContext is AlbumViewModel vm)
        {
            vm.AppendCommand.Execute(null);
        }
        e.Handled = true;
    }
    private void AlbumItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Handled) return;
        if (sender is Border border && border.DataContext is AlbumViewModel album)
        {
            album.DoubleTappedCommand.Execute(null);
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is AlbumsListViewModel vm )
            vm.ScrollOffset = Scroller.Offset.Y;
    }

    public void SetScrollOffset(double scrollOffset)
    {
        Dispatcher.UIThread.Post(() => 
                Scroller.Offset = new Vector(Scroller.Offset.X, scrollOffset),
            DispatcherPriority.Loaded);
        
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_oldvm != null)
        {
            _oldvm.ScrollToOffset = null;
            _oldvm = null;
        }
            
        if (DataContext is AlbumsListViewModel vm)
        {
            vm.ScrollToOffset = SetScrollOffset;
            _oldvm = vm;
        }
    }

    private void ItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if(e.Handled) return;
        if (sender is Border border && border.DataContext is AlbumViewModel vm && !vm.IsSelected)
        {
            var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
            if ( kind is PointerUpdateKind.RightButtonPressed && ! vm.IsSelected)
                vm.IsSelected = true;
        }
    }



    private void DeviceMenuItemPressed(object? sender, PointerPressedEventArgs e)
    {
        if(sender is Border border && 
           border.DataContext is ExternalDevice device && 
           device.IsPlugged &&
           border.Parent?.Parent is MenuItem mi &&
           mi.Tag is TracksGroupViewModel vm)
        {
            _ = vm.AddToDevice(device);
        }
    }
}