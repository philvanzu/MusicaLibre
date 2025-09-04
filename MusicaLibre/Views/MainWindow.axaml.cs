using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class MainWindow : WindowBase
{
    public MainWindow()
    {
        InitializeComponent();
        InputManager.Instance.Attach(this);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is MainWindowViewModel vm)
            vm.OnWindowOpened();
        
        
        //Start on primary screen
        /*
        var state = AppData.Instance.AppState;
        var primary = Screens.Primary;   // always exists
        var area = primary.WorkingArea;


        // center it on the primary screen
        Position = new PixelPoint( area.X + state.WindowPosition.X, area.Y + state.WindowPosition.Y );
        Width = state.WindowWidth;
        Height = state.WindowHeight;
        WindowState = state.WindowState;
        */   
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if(DataContext is MainWindowViewModel vm)
            vm.OnWindowClosing();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        try
        {
            var appState = AppData.Instance.AppState;
            appState.WindowPosition = Position;
            appState.WindowWidth = Width;
            appState.WindowHeight = Height;
            appState.WindowState = WindowState;
            AppData.Instance.AppState = appState;
            AppData.Instance.Save();
        }
        catch (Exception ex){Console.WriteLine(ex);}
    }
}