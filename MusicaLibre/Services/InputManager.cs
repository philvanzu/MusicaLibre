using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace MusicaLibre.Services;

public class InputManager
{
    private static readonly Lazy<InputManager> _instance =
        new(() => new InputManager());

    public static InputManager Instance => _instance.Value;

    private InputManager()
    {
        AppFocusManager.Instance.AppLostFocus += ((sender, args) =>
        {
            CtrlPressed = false;
            IsDragSelecting = false;
        });
    }


    public static bool CtrlPressed;
    public static bool ShiftPressed;
    public static bool IsDragSelecting;
    public void Attach(Window window)
    {
        /*
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>(OnKeyDown, handledEventsToo: true);
        InputElement.KeyUpEvent.AddClassHandler<TopLevel>(OnKeyUp, handledEventsToo: true);
        */
        window.KeyDown += OnKeyDown;
        window.KeyUp += OnKeyUp;
        window.Closing += OnAttachedWindowClosing;
    }

    private void OnAttachedWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (sender is Window window)
        {
            window.KeyDown -= OnKeyDown;
            window.KeyUp -= OnKeyUp;
            window.Closing -= OnAttachedWindowClosing;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) CtrlPressed = true;
        if (e.Key == Key.LeftShift || e.Key == Key.RightShift) ShiftPressed = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if(e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) CtrlPressed = false;
        if (e.Key == Key.LeftShift || e.Key == Key.RightShift) ShiftPressed = false;
    }
}


