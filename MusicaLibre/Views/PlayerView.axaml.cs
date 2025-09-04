using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MusicaLibre.ViewModels;
using Avalonia.Controls.Primitives;
using MusicaLibre.Controls;

namespace MusicaLibre.Views;

public partial class PlayerView : UserControl
{
    public PlayerView()
    {
        InitializeComponent();
    }





    private void Slider_DragStarted(object? sender, EventArgs e)
    {
        if (DataContext is PlayerViewModel vm)
        {
            vm.IsSeeking = true;
        }
    }

    private void Slider_DragCompleted(object? sender, EventArgs e)
    {
        if (sender is SeekSlider slider && DataContext is PlayerViewModel vm)
        {
            vm.IsSeeking = false;
            //vm.Position = slider.Value; // jump to final position
            vm.SetTrackPosition(slider.Position);
        }
    }
}