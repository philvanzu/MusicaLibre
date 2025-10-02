using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using MusicaLibre.Controls;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class TracksListView : UserControl
{
    private TracksListViewModel? _oldvm;
    public TracksListView()
    {
        InitializeComponent();
    }

    private void TrackItemDoubleTapped(object? s, TappedEventArgs e)
    {
        Console.WriteLine("RowDoubleTapped");
        if (e.Handled) return;
        if (s is Border b && b.DataContext is TrackViewModel vm)
            vm.DoubleTappedCommand.Execute(null);
        e.Handled = true;
    }

    private void TrackItemPropertyChanged(object? s, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TagProperty && s is Border b && b.DataContext is TrackViewModel vm) //IsSelected changed
        {
            b.Background = vm.IsSelected ? TrackGrid.SelectedBrush : vm.EvenRow? TrackGrid.EvenRowBrush : TrackGrid.OddRowBrush;
            b.BorderThickness= vm.IsSelected ? new Thickness(1) : new Thickness(0);
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is TracksListViewModel vm )
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
            
        if (DataContext is TracksListViewModel vm)
        {
            vm.ScrollToOffset = SetScrollOffset;
            _oldvm = vm;
        }
    }

    private void ItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if(e.Handled) return;
        if (sender is Border border && border.DataContext is TrackViewModel vm && !vm.IsSelected)
        {
            var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
            if ( kind is PointerUpdateKind.RightButtonPressed)
            {
                var ctrlPressed = InputManager.CtrlPressed;
                InputManager.CtrlPressed = true;
                vm.IsSelected = true;
                InputManager.CtrlPressed = ctrlPressed;
            }
        }
    }
}