using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class PlaylistsListView : UserControl
{
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
   
}