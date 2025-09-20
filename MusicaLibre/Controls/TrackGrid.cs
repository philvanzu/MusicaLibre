using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Collections;
using Avalonia.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using MusicaLibre.Converters;
using MusicaLibre.Services;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Controls;
public class TrackGrid : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<object>?> ItemsSourceProperty =
        AvaloniaProperty.Register<TrackGrid, IEnumerable<object>?>(nameof(ItemsSource));

    public static readonly StyledProperty<List<TrackViewColumn>?> ColumnsProperty =
        AvaloniaProperty.Register<TrackGrid, List<TrackViewColumn>?>(nameof(Columns));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<TrackGrid, object?>(nameof(SelectedItem));

    public static SolidColorBrush EvenRowBrush = new SolidColorBrush(new Color(255, 33, 33, 33));
    public static SolidColorBrush OddRowBrush = new SolidColorBrush(new Color(255, 44, 44, 44));
    private static SolidColorBrush _headerBrush = new SolidColorBrush(new Color(255, 222, 211, 151));
    public static SolidColorBrush SelectedBrush = new SolidColorBrush(new Color(80, 80, 255, 51));
    private static SolidColorBrush _selectionRectFillBrush = new SolidColorBrush(new Color(64, 0, 120, 215)); // translucent
    private static Pen _selectionRectPen = new Pen(Brushes.Blue, 1);
    public IEnumerable<object>? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public List<TrackViewColumn>? Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }
    


    List<Border> _headerToggleBorders = new List<Border>();
    List<Border> _headerBorders = new List<Border>();
    
    private readonly HashSet<Control> _realizedElements = new();
    private ItemsRepeater? _repeater;
    private Grid? _headerGrid;
    
    private Point? _pressPoint;
    private DateTime? _pressTime;
    private Point? _dragEnd;
    private bool _isSelecting;
    private Rect? _selectionRect;


    public TrackGrid()
    {
        Focusable = true;
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColumnsProperty)
        {
            if (Columns != null)
            {
                if( _headerGrid != null) BuildHeader(_headerGrid, Columns);
                //rows
            }
        }
    }



    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Focus();
        base.OnApplyTemplate(e);
        _repeater = e.NameScope.Find<ItemsRepeater>("PART_Repeater");
        
        _headerGrid = e.NameScope.Find<Grid>("PART_Header");
        
        
        if (Columns is not null)
        {
            if (_headerGrid is not null) BuildHeader(_headerGrid, Columns);
            //if(_rowsGrid is not null)BuildRow(_rowsGrid, Columns);
        }
        
        if (_repeater is not null)
        {
            _repeater.ElementPrepared += OnRowPrepared;
            _repeater.ElementClearing += OnRowClearing;
        }
        
    }



    private void OnRowPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
    {
        if (e.Element.DataContext is TrackViewModel track && Columns is not null)
        {
            var rowGrid = e.Element.GetVisualDescendants().OfType<Grid>()
                .FirstOrDefault(g => g.Name == "PART_RowGrid");
 
            if(rowGrid is not null ) 
                BuildRow(rowGrid, Columns, track);    
        }
        
        if(e.Element is { } c)
            _realizedElements.Add(c);
    }
    private void OnRowClearing(object? sender, ItemsRepeaterElementClearingEventArgs e)
    {
        if(e.Element is { } c)
            _realizedElements.Remove(c);
    }
    
    #region Header
    private void BuildHeader(Grid grid, List<TrackViewColumn> columns)
    {
        grid.ColumnDefinitions.Clear();
        grid.Children.Clear();
        
        var colIndex = 0;
        foreach (var column in columns)
        {
            if (!column.IsVisible) continue;
            
            // add column definition
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = double.IsNaN(column.Width)
                    ? GridLength.Auto
                    : new GridLength(column.Width, GridUnitType.Pixel),
                SharedSizeGroup = column.Key
            });
            
            var cellHeader = BuildCellHeader(column);
            Grid.SetColumn(cellHeader, colIndex * 2);
            grid.Children.Add(cellHeader);

            // add splitter (optional: skip last column)
            
            if (colIndex < columns.Count() - 1)
            {
                // add column definition
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8,  GridUnitType.Pixel) });
                var splitter = new GridSplitter
                {
                    Width = 6,
                    ResizeDirection = GridResizeDirection.Columns,
                    ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                    Background = Brushes.Transparent,
                    Cursor = new Cursor(StandardCursorType.SizeWestEast),
                };
                Grid.SetColumn(splitter, colIndex*2 + 1);
                grid.Children.Add(splitter);
            }

            colIndex++;
        }
    }

private Control BuildCellHeader(TrackViewColumn column)
{
    var grid = new Grid
    {
        ColumnDefinitions =
        {
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, // sort by column
            new ColumnDefinition { Width = GridLength.Auto }                       // ascending toggle
        },
        DataContext = column
    };

    // Left side: column title (tap = set IsSorting)
    var sortBorder = new Border
    {
        DataContext = column,
        Height = 32,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        BorderBrush = Brushes.DodgerBlue,
        BorderThickness = new Thickness(1, 0, 0, 0),
        Background = Brushes.Transparent,
        Padding = new Thickness(4, 0),
        Child = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.Bold,
            Foreground = _headerBrush,
        }
    };
    sortBorder.Child.Bind(TextBlock.TextProperty, new Binding(nameof(TrackViewColumn.Key)));
    _headerBorders.Add(sortBorder);
    
    Grid.SetColumn(sortBorder, 0);
    grid.Children.Add(sortBorder);

    // Right side: sort direction toggle (only visible when IsSorting = true)
    var toggleBorder = new Border
    {
        DataContext = column,
        Background = Brushes.Transparent,
        Padding = new Thickness(4, 0),
        Child = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.Bold,
            Foreground = _headerBrush,
        }
    };

    // bind visibility to IsSorting
    toggleBorder.Bind(IsVisibleProperty, new Binding(nameof(TrackViewColumn.IsSorting)));

    // bind arrow to IsAscending
    toggleBorder.Child.Bind(TextBlock.TextProperty, new Binding(nameof(TrackViewColumn.IsAscending))
    {
        Converter = new FuncValueConverter<bool, string>(asc => asc ? "▲" : "▼")
    });
    _headerToggleBorders.Add(toggleBorder);
    
    Grid.SetColumn(toggleBorder, 1);
    grid.Children.Add(toggleBorder);

    return grid;
}
#endregion

#region Rows
    public void BuildRow(Grid grid, List<TrackViewColumn> columns, TrackViewModel track)
    {
        grid.ColumnDefinitions.Clear();
        grid.Children.Clear();
        

        if (grid.Parent is Border border)
        {
            border.Background = track.EvenRow? EvenRowBrush : OddRowBrush;
        }
        
        var colIndex = 0;
        foreach (var column in columns)
        {
            if (!column.IsVisible) continue;
            
            // add column definition
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = double.IsNaN(column.Width)
                    ? GridLength.Auto
                    : new GridLength(column.Width, GridUnitType.Pixel),
                SharedSizeGroup = column.Key
            });

            
            
            var txt = new TextBlock
            {
                Text = column.GetRowText(track),
                FontWeight = column.Key == "Title" ? FontWeight.Bold : FontWeight.Normal,
                FontSize = column.Key == "Title" ? 16 : 14,
                HorizontalAlignment = column.IsCentered ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (column.ToolTipGetter != null)
            {
                var tooltip = column.ToolTipGetter.Invoke(track);
                if(!string.IsNullOrEmpty(tooltip))
                    ToolTip.SetTip(txt, tooltip);    
            }
            
            
            var cellBorder = new Border
            {
                //Background = track.IsSelected? _selectedBrush :evenRow ? _evenCellBrush : _oddCellBrush,
                Padding = new Thickness(4, 0),
                Child = txt
            };

            Grid.SetColumn(cellBorder, colIndex * 2);
            grid.Children.Add(cellBorder);

            // add splitter (optional: skip last column)
            if (colIndex < columns.Count() - 1)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            
            colIndex++;
        }
    }
  #endregion 
  
    // === DRAG SELECTION HANDLERS ===
    
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            
            
            _pressPoint = e.GetPosition(this);
            _pressTime = DateTime.Now;
            
            e.Pointer.Capture(this);

            InvalidateVisual();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pressPoint.HasValue)
        {
            if (!_isSelecting)
            {
                var delta = e.GetPosition(this) - _pressPoint.Value;
                var magnitudeSquared = delta.X * delta.X + delta.Y * delta.Y;
                if (magnitudeSquared > 16) // 16 = 4 pixels threshold squared
                {
                    _dragEnd = _pressPoint;
                    InputManager.IsDragSelecting = true;
                    _isSelecting = true;
                    _selectionRect = new Rect(_pressPoint.Value, _dragEnd.Value);
                }
            }
            
            if( _isSelecting )
            {
                _dragEnd = e.GetPosition(this);

                var x = Math.Min(_pressPoint.Value.X, _dragEnd.Value.X);
                var y = Math.Min(_pressPoint.Value.Y, _dragEnd.Value.Y);
                var w = Math.Abs(_pressPoint.Value.X - _dragEnd.Value.X);
                var h = Math.Abs(_pressPoint.Value.Y - _dragEnd.Value.Y);

                _selectionRect = new Rect(x, y, w, h);
                InvalidateVisual(); // just redraw
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
        {
            if (_isSelecting && _pressPoint.HasValue && _selectionRect.HasValue)
            {
                // apply selection ONCE here
                ApplySelection(_selectionRect.Value);
                InvalidateVisual();
            }
            else 
            {
                var track = TrackUnderPointer(e.GetPosition(this));
                if (track != null)
                {
                    if (InputManager.CtrlPressed && track.IsSelected) track.IsSelected = false;
                    else
                    {
                        if (track.IsSelected && !InputManager.ShiftPressed) track.Presenter.SelectedItem = null;
                        track.IsSelected = true;
                    }
                }
                else
                {
                    var headercol = HeaderCellUnderPointer(e.GetPosition(this), _headerBorders);
                    if (headercol != null && headercol.DataContext is TrackViewColumn coldc)
                    {
                        coldc.IsSorting = true;
                    }
                    else
                    {
                        var headerToggle = HeaderCellUnderPointer(e.GetPosition(this), _headerToggleBorders);
                        if (headerToggle != null && headerToggle.DataContext is TrackViewColumn col)
                        {
                            col.IsAscending = !col.IsAscending;
                        }        
                    }
                }
            }
        
            InputManager.IsDragSelecting = false;
            _selectionRect = null;
            _pressPoint = null;
            _dragEnd = null;
            _isSelecting = false;
            e.Pointer.Capture(null);

        }
    }

    private void ApplySelection(Rect rect)
    {
        foreach (var element in _realizedElements)
        {
            if (element.DataContext is TrackViewModel vm)
            {
                var offset = element.TranslatePoint(new Point(0, 0), this) ?? default;
                var rowRect = new Rect(offset, element.Bounds.Size);

                vm.IsSelected = rect.Intersects(rowRect);
            }
        }
    }

    private TrackViewModel? TrackUnderPointer(Point point)
    {
        foreach (var element in _realizedElements)
        {
            if (element.DataContext is not TrackViewModel vm) continue;
            var offset = element.TranslatePoint(new Point(0, 0), this) ?? default;
            var rowRect = new Rect(offset, element.Bounds.Size);
            if(rowRect.Contains(point)) return vm;
        } 
        return null;
    }
    
    private Border? HeaderCellUnderPointer(Point point, List<Border> pool)
    {
        if (_headerGrid == null || Columns == null) return null;
        foreach (var cell in pool)
        {
            if (cell.DataContext is TrackViewColumn col)
            {
                // Get bounding box of the header cell
                var topLeft = cell.TranslatePoint(new Point(0, 0), this) ?? default;
                var rect = new Rect(topLeft, cell.Bounds.Size);

                if (rect.Contains(point))
                    return cell;
            }
        }

        return null;
    }
    
    public override void Render(DrawingContext context)
    {
        
        if (_selectionRect.HasValue)
        {
            context.FillRectangle(_selectionRectFillBrush, _selectionRect.Value);
            context.DrawRectangle(_selectionRectPen, _selectionRect.Value);    
        }
        base.Render(context);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.A && e.KeyModifiers == KeyModifiers.Control && ItemsSource != null)
        {
            foreach (var item in ItemsSource)
            {
                if (item is TrackViewModel vm)
                    vm.IsSelected = true;
            }
            e.Handled = true;
        }
    }
}