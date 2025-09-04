using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class AlbumsListView : UserControl
{
    
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
            else album.IsSelected = true;
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

    private void AlbumItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Handled) return;
        if (sender is Border border && border.DataContext is AlbumViewModel album)
        {
            album.DoubleTappedCommand.Execute(null);
        }
    }
}