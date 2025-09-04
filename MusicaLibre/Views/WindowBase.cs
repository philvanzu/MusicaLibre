namespace MusicaLibre.Views;

using Avalonia.Controls;
using MusicaLibre.Services;


public class WindowBase: Window
{
    public WindowBase()
    {
        // Register this window with the AppFocusManager when it's created
        AppFocusManager.Instance.RegisterWindow(this);
    }
}