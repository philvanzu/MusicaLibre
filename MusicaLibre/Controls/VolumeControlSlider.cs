using System;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia;
using Avalonia.Controls;
namespace MusicaLibre.Controls;

public class VolumeControlSlider:Control
{
    private static readonly SolidColorBrush GreyFillBrush = new SolidColorBrush(Color.FromArgb(255, 90, 100, 100));
    private static readonly SolidColorBrush WhiteFillBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
    
    const double _trackHeight = 24;           // fixed track height
    
    bool _isDragging;
    
    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<VolumeControlSlider, double>(nameof(Volume));
    public double Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public VolumeControlSlider()
    {
        this.GetObservable(VolumeProperty).Subscribe(_ => InvalidateVisual());
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
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled) return;
        var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        if (kind == PointerUpdateKind.LeftButtonPressed )
        {
            _isDragging = true;
            OnDrag(e);
        }
    }

    void OnDrag( PointerEventArgs e)
    {
        var pointerX = e.GetPosition(this).X;
        var trackWidth = Bounds.Width ;
        var pos = Math.Clamp(pointerX, 0, trackWidth);
        Volume = pos /  trackWidth;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Bounds.Width;
        var height = _trackHeight;
        var volumeWidth = width * Volume;

        // Grey background triangle (right triangle)
        var greyTriangle = new StreamGeometry();
        using (var ctx = greyTriangle.Open())
        {
            ctx.BeginFigure(new Point(0, height), true);       // bottom-left (right angle)
            ctx.LineTo(new Point(width, height));             // bottom-right
            ctx.LineTo(new Point(width, 0));                 // top-right
            ctx.EndFigure(true);
        }
        context.DrawGeometry(GreyFillBrush, null, greyTriangle);

        // Blue triangle (scaled proportionally)
        var blueHeight = volumeWidth * height / width;       // similar triangle formula
        var blueTriangle = new StreamGeometry();
        using (var ctx = blueTriangle.Open())
        {
            ctx.BeginFigure(new Point(0, height), true);             // bottom-left
            ctx.LineTo(new Point(volumeWidth, height), true);       // bottom-right (right angle)
            ctx.LineTo(new Point(volumeWidth, height - blueHeight), true); // top-right
            ctx.EndFigure(true);
        }
        context.DrawGeometry(WhiteFillBrush, null, blueTriangle);

    }

}