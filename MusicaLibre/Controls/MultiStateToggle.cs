using System.Collections.Generic;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Math = System.Math;

namespace MusicaLibre.Controls;

public class MultiStateToggle : TemplatedControl
{
    public static readonly StyledProperty<int> CurrentStateIndexProperty =
        AvaloniaProperty.Register<MultiStateToggle, int>(nameof(CurrentStateIndex));

    public int CurrentStateIndex
    {
        get => GetValue(CurrentStateIndexProperty);
        set => SetValue(CurrentStateIndexProperty, value);
    }

    public static readonly StyledProperty<IList<ToggleState>> StatesProperty =
        AvaloniaProperty.Register<MultiStateToggle, IList<ToggleState>>(nameof(States));

    public IList<ToggleState> States
    {
        get => GetValue(StatesProperty);
        set => SetValue(StatesProperty, value);
    }

    private Path? _path;
    private bool _isPointerOver;
    private bool _isPressed;

    static MultiStateToggle()
    {
    }

    public MultiStateToggle()
    {
        States = new AvaloniaList<ToggleState>();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == StatesProperty || change.Property == CurrentStateIndexProperty)
            InvalidateVisualsForState(change);
        
        else if (change.Property == IsEnabledProperty)
            UpdatePathFill();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _path = e.NameScope.Find<Path>("PART_path");
        UpdatePathVisuals();
    }

    private void InvalidateVisualsForState(AvaloniaPropertyChangedEventArgs e) => UpdatePathVisuals();

    private void UpdatePathVisuals()
    {
        if (_path == null || States.Count == 0) return;

        var state = States[Math.Clamp(CurrentStateIndex, 0, States.Count - 1)];
        _path.Data = state.Icon;
        ToolTip.SetTip(this, state.ToolTip);
        UpdatePathFill();
    }

    private void UpdatePathFill()
    {
        if (_path == null || States.Count == 0) return;

        var state = States[Math.Clamp(CurrentStateIndex, 0, States.Count - 1)];

        if (!IsEnabled)
            _path.Fill = state.Disabled ?? state.Fill;
        else if (_isPressed)
            _path.Fill = state.Pressed ?? state.Fill;
        else if (_isPointerOver)
            _path.Fill = state.PointerOver ?? state.Fill;
        else
            _path.Fill = state.Fill;
    }

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

            // Advance state
            if (States.Count > 0)
                CurrentStateIndex = (CurrentStateIndex + 1) % States.Count;

            UpdatePathVisuals();
        }
    }
}


public class ToggleState
{
    public Geometry? Icon { get; set; }
    public IBrush? Fill { get; set; }
    public IBrush? PointerOver { get; set; }
    public IBrush? Pressed { get; set; }
    public IBrush? Disabled { get; set; }
    public string? ToolTip { get; set; }
}