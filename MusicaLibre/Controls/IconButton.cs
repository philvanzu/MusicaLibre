using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace MusicaLibre.Controls;

public class IconButton:TemplatedControl
{
    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<IconButton, Geometry?>(nameof(IconData));
    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(Fill));
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }
    
    public static readonly StyledProperty<IBrush?> PointerOverProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(PointerOver));
    public IBrush? PointerOver
    {
        get => GetValue(PointerOverProperty);
        set => SetValue(PointerOverProperty, value);
    }
    
    public static readonly StyledProperty<IBrush?> PressedProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(Pressed));
    public IBrush? Pressed
    {
        get => GetValue(PressedProperty);
        set => SetValue(PressedProperty, value);
    }
    
    public static readonly StyledProperty<IBrush?> DisabledProperty =
        AvaloniaProperty.Register<IconButton, IBrush?>(nameof(Disabled));
    public IBrush? Disabled
    {
        get => GetValue(DisabledProperty);
        set => SetValue(DisabledProperty, value);
    }
    
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<IconButton, ICommand?>(nameof(Command));
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private Path? _path;
    
    public IconButton()
    {
        this.GetObservable(IsEnabledProperty).Subscribe(_ => UpdatePathFill());
    }
    
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        
        _path = e.NameScope.Find<Path>("PART_path");

        if (_path != null)
        {
            _path.Data = IconData;
            UpdatePathFill();
        }
    }
    private void UpdatePathFill()
    {
        if (_path is null) 
            return;
        if (!IsEnabled)
            _path!.Fill = Disabled ?? Fill;
        else if (_isPressed)
            _path!.Fill = Pressed ?? Fill;
        else if (_isPointerOver)
            _path!.Fill = PointerOver ?? Fill;
        else
            _path!.Fill = Fill;
    }

    private bool _isPointerOver;
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isPointerOver = true;
        UpdatePathFill();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerOver = false;
        UpdatePathFill();
    }

    private bool _isPressed;
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!IsEnabled)
            return;

        e.Pointer.Capture(this);
        _isPressed = true;
        UpdatePathFill();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isPressed && e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
            _isPressed = false;
            UpdatePathFill();

            // Fire command if available
            if (Command?.CanExecute(null) ?? false)
                Command.Execute(null);
        }
    }

    

    
}