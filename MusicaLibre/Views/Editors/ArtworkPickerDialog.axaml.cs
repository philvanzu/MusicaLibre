using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MusicaLibre.Models;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class ArtworkPickerDialog : Window
{
    public ArtworkPickerDialog()
    {
        InitializeComponent();
    }

    private void ArtworkTapped(object? sender, TappedEventArgs e)
    {
        if(sender is Border border && border.DataContext is ArtworkViewModel vm)
            vm.IsSelected = true;
    }
}