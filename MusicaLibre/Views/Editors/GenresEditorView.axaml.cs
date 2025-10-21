using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class GenresEditorView : UserControl
{
    public GenresEditorView()
    {
        InitializeComponent();
    }
    
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (DataContext is GenresEditorViewModel vm)
        {
            if (e.Key == Key.Down){vm.ArrowDownCommand.Execute(null);}
            if (e.Key == Key.Up){vm.ArrowUpCommand.Execute(null);}    
        }
    }
}