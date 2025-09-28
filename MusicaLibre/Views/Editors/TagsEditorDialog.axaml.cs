using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using MusicaLibre.Controls;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class TagsEditorDialog : Window
{
    SolidColorBrush _selectedBg =  new SolidColorBrush(Colors.DarkBlue);
    public TagsEditorDialog()
    {
        InitializeComponent();
    }

    private void TrackItemPropertyChanged(object? s, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TagProperty && s is Border b && b.DataContext is TrackViewModel vm) //IsSelected changed
        {
            b.Background = vm.IsSelected ? _selectedBg :
                vm.EvenRow ? TrackGrid.EvenRowBrush : TrackGrid.OddRowBrush;
            b.BorderThickness = vm.IsSelected ? new Thickness(1) : new Thickness(0);
        }
    }
}