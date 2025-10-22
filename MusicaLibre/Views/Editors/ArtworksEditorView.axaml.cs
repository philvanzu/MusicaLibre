using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class ArtworksEditorView : UserControl
{
    public ArtworksEditorView()
    {
        InitializeComponent();
    }

    private void OnFilterBoxKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is ArtworksEditorViewModel vm)
        {
            if (e.Key == Key.Down){vm.ArrowDownCommand.Execute(null);}
            if (e.Key == Key.Up){vm.ArrowUpCommand.Execute(null);}  
            
        }
    }
}