using System;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia;
using Avalonia.Controls;

namespace MusicaLibre.Controls;
public class SeekSlider : UserControl
{
    private static readonly SolidColorBrush GreyFillBrush = new SolidColorBrush(Color.FromArgb(255, 90, 100, 100));
    private static readonly SolidColorBrush BlueFillBrush = new SolidColorBrush(Color.FromArgb(64, 0, 120, 215));
    
    public event EventHandler? DragStarted;
    public event EventHandler? DragCompleted;
    
    const double _trackHeight = 12;           // fixed track height
    const double _circleDiameter = 16; 
    const double _circleRadius = 8;
    
    bool _isDragging;
    
    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<SeekSlider, double>(nameof(Position));
    public double Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public SeekSlider()
    {
        this.GetObservable(PositionProperty).Subscribe(_ => InvalidateVisual());
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsEnabled || !_isDragging) return;
        
        OnDrag(e);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsEnabled || !_isDragging) return;
        
        var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        if (kind == PointerUpdateKind.LeftButtonReleased)
        {
            _isDragging = false;
            DragCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled) return;
        var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        if (kind == PointerUpdateKind.LeftButtonPressed )
        {
            DragStarted?.Invoke(this, EventArgs.Empty);
            _isDragging = true;
            OnDrag(e);
        }
    }

    void OnDrag( PointerEventArgs e)
    {
        var pointerX = e.GetPosition(this).X;
        var trackWidth = Bounds.Width - _circleDiameter * 1.5;
        var pos = Math.Clamp(pointerX, _circleDiameter, trackWidth+_circleDiameter);
        Position = (pos - _circleDiameter) /  trackWidth;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var trackWidth = Bounds.Width - _circleDiameter * 1.5;
        double filledWidth = trackWidth * Position;
        double trackY = (Bounds.Height - _trackHeight) / 2;  // center vertically
        double trackX = _circleRadius;
        double knobX = filledWidth + trackX;
        double knobY = (Bounds.Height - _circleDiameter) / 2;
        // Left filled rectangle
        context.FillRectangle(BlueFillBrush, new Rect(trackX, trackY, filledWidth, _trackHeight), 6);

        // Right background rectangle
        context.FillRectangle(GreyFillBrush, new Rect(knobX, trackY, trackWidth - filledWidth+_circleRadius, _trackHeight), 6);

        // Circle knob on top
        var circleRect = new Rect(knobX, knobY, _circleDiameter, _circleDiameter);
        context.DrawGeometry(Brushes.White, null, new EllipseGeometry(circleRect));

    }
}