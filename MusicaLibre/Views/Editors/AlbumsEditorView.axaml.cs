using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class AlbumsEditorView : UserControl
{
    public AlbumsEditorView()
    {
        InitializeComponent();
    }
    
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (DataContext is AlbumsEditorViewModel vm)
        {
            if (e.Key == Key.Down){vm.ArrowDownCommand.Execute(null);}
            if (e.Key == Key.Up){vm.ArrowUpCommand.Execute(null);}    
        }
    }
}