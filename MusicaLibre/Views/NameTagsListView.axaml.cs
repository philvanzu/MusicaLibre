using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class NameTagsListView : UserControl
{
    public NameTagsListView()
    {
        InitializeComponent();
    }

    private void ItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Handled) return;
        if (sender is Border border && border.DataContext is NameTagViewModelBase vm)
        {
            if (vm.IsSelected && InputManager.CtrlPressed) vm.IsSelected = false;
            else vm.IsSelected = true;
        }
    }

    private void ItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Handled) return;
        if (sender is Border border && border.DataContext is NameTagViewModelBase vm)
        {
            vm.DoubleTappedCommand.Execute(null);
        }
        e.Handled = true;
    }

    private void PlayButton_Click(object? sender, TappedEventArgs e)
    {
        if (sender is Button button && button.DataContext is NameTagViewModelBase vm)
        {
            vm.PlayCommand.Execute(null);
        }
        e.Handled = true;
    }
    private void InsertButton_Click(object? sender, TappedEventArgs e)
    {
        if (sender is Button button && button.DataContext is NameTagViewModelBase vm)
        {
            vm.InsertNextCommand.Execute(null);
        }
        e.Handled = true;
    }

    private void AppendButton_Click(object? sender, TappedEventArgs e)
    {
        if (sender is Button button && button.DataContext is NameTagViewModelBase vm)
        {
            vm.AppendCommand.Execute(null);
        }
        e.Handled = true;
    }
}