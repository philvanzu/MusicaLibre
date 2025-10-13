using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class PlaylistsListView : UserControl
{
    private PlaylistsListViewModel? _oldvm;
    public PlaylistsListView()
    {
        InitializeComponent();
    }

    private void PlaylistItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Handled) return;
        if (sender is Border border && border.DataContext is PlaylistViewModel vm)
        {
            if (vm.IsSelected && InputManager.CtrlPressed) vm.IsSelected = false;
            else vm.IsSelected = true;
        }
    }
    private void PlaylistItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Handled) return;
        if (sender is Border border && border.DataContext is PlaylistViewModel vm)
        {
            vm.DoubleTappedCommand.Execute(null);
        }
        e.Handled = true;
    }

    private void PlayButton_Click(object? sender, TappedEventArgs e)
    {
        if (sender is Button button && button.DataContext is PlaylistViewModel vm)
        {
            vm.PlayCommand.Execute(null);
        }
        e.Handled = true;
    }

    private void InsertButton_Click(object? sender, TappedEventArgs e)
    {
        if (sender is Button button && button.DataContext is PlaylistViewModel vm)
        {
            vm.InsertNextCommand.Execute(null);
        }
        e.Handled = true;
    }

    private void AppendButton_Click(object? sender, TappedEventArgs e)
    {
        if (sender is Button button && button.DataContext is PlaylistViewModel vm)
        {
            vm.AppendCommand.Execute(null);
        }
        e.Handled = true;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is PlaylistsListViewModel vm )
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
            
        if (DataContext is PlaylistsListViewModel vm)
        {
            vm.ScrollToOffset = SetScrollOffset;
            _oldvm = vm;
        }
    }

    private void PlaylistItemClicked(object? sender, PointerPressedEventArgs e)
    {
        if(e.Handled) return;
        if (sender is Border border && border.DataContext is PlaylistViewModel vm && !vm.IsSelected)
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