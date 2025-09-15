using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    private void OnSearBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is LibraryViewModel vm)
        {
            if (e.Key == Key.Enter)
            {
                vm.SearchCommand.Execute(null);
                e.Handled = true;
            }
/*
            if (e.Key == Key.Escape)
            {
                
                e.Handled = true;
            }
*/
        }
    }
}