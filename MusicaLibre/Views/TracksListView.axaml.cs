using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using MusicaLibre.Controls;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class TracksListView : UserControl
{
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
}