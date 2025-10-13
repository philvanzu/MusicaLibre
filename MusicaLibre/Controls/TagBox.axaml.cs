using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicaLibre.Controls;

public partial class TagBox : UserControl
{
    private List<string> _commitedValues=new List<string>();
    private string? _currentValue;
    private int _optionsCount = 0;

    private static readonly StyledProperty<bool> PopupIsOpenProperty =
        AvaloniaProperty.Register<TagBox, bool>(nameof(PopupIsOpen), defaultBindingMode: BindingMode.TwoWay);
    public bool PopupIsOpen
    {
        get => GetValue(PopupIsOpenProperty);
        set => SetValue(PopupIsOpenProperty, value);
    }
    
    private static readonly StyledProperty<bool> PopupEnabledProperty =
        AvaloniaProperty.Register<TagBox, bool>(nameof(PopupEnabled));
    public bool PopupEnabled
    {
        get => GetValue(PopupEnabledProperty);
        set => SetValue(PopupEnabledProperty, value);
    }
    
    public static readonly StyledProperty<IEnumerable<string>?> OptionsProperty =
        AvaloniaProperty.Register<TagBox, IEnumerable<string>?>(nameof(Options));
    public IEnumerable<string>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }
    
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TagBox, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<bool> IsMultiValueProperty =
        AvaloniaProperty.Register<TagBox, bool>(nameof(IsMultiValue));
    public bool IsMultiValue
    {
        get => GetValue(IsMultiValueProperty);
        set => SetValue(IsMultiValueProperty, value);
    }

    public static readonly StyledProperty<ICommand?> EnterPressedCommandProperty =
        AvaloniaProperty.Register<TagBox, ICommand?>(nameof(EnterPressedCommand));

    public ICommand? EnterPressedCommand
    {
        get => GetValue(EnterPressedCommandProperty);
        set => SetValue(EnterPressedCommandProperty, value);
    }
    
    public TagBox()
    {
        InitializeComponent();
        PART_Listbox.PropertyChanged += ListBoxPropertyChanged;
        PART_Textbox.TextChanged += TextBoxTextChanged;
        PART_Textbox.KeyDown += TextBoxKeyDown;
        PART_Listbox.KeyDown += ListBoxKeyDown;
    }

    private void TextBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsMultiValue)
        {
            _commitedValues.Clear();
            var split = PART_Textbox.Text?.Split(",");
            if (split != null && split.Length > 0)
            {
                _currentValue = split[split.Length - 1].Trim();
                    
                for (int i = 0; i < split.Length-1; i++)
                    _commitedValues.Add(split[i].Trim());
            }
        }
        else _currentValue = PART_Textbox.Text?.Trim();

        PART_Popup.IsOpen = PART_Textbox.IsFocused && Options?.Count() > 0;
    }

    private void ListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;

            if (PART_Listbox.SelectedIndex >= 0 && Options != null)
            {
                _currentValue = Options.ElementAt(PART_Listbox.SelectedIndex);
                if (IsMultiValue)
                {
                    var text = (_commitedValues.Count > 0) ? $"{string.Join(", ", _commitedValues)}, " : string.Empty;
                    PART_Textbox.Text = $"{text}{_currentValue}";
                }
                    
                else
                    PART_Textbox.Text = _currentValue;

                PopupIsOpen = false;
                PART_Textbox.Focus(); // return focus to TextBox
                PART_Textbox.CaretIndex = PART_Textbox.Text?.Length ?? 0;
            }
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            PopupIsOpen = false;
            PART_Textbox.Focus(); // return focus to TextBox
            PART_Textbox.CaretIndex = PART_Textbox.Text?.Length ?? 0;
        }
    }

    private void TextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            EnterPressedCommand?.Execute(null);
        }

        if (e.Key == Key.Escape)
        {
            PopupIsOpen = false;
        }

        if (e.Key == Key.Up)
        {
            e.Handled = true;
            if (PART_Listbox.SelectedIndex > 0)
                PART_Listbox.SelectedIndex--;
        }

        if (e.Key == Key.Down)
        {
            if (PopupEnabled)
            {
                PopupIsOpen = true;
                if (PART_Listbox.SelectedIndex < 0)
                    PART_Listbox.SelectedIndex = 0;
                else if (PART_Listbox.SelectedIndex < _optionsCount - 1)
                    PART_Listbox.SelectedIndex++;
                
//                PART_Listbox.Focus(); // move keyboard focus to the ListBox
            }
        }
    }


    private void ListBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SelectingItemsControl.SelectedIndexProperty && (Options?.Any()==true))
        {
            if (PART_Listbox.SelectedIndex >= 0 && PART_Listbox.SelectedIndex < _optionsCount)
            {
                _currentValue = Options.ElementAt(PART_Listbox.SelectedIndex);
                if (IsMultiValue)
                {
                    var text = (_commitedValues.Count > 0) ? $"{string.Join(", ", _commitedValues)}, " : string.Empty;
                    PART_Textbox.Text = $"{text}{_currentValue}";
                }
                else PART_Textbox.Text = _currentValue;
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OptionsProperty)
        {
            _optionsCount = Options!=null? Options.Count() : 0;
            PopupEnabled = _optionsCount > 0;
        }
    }

    void TogglePopup(object? sender, RoutedEventArgs routedEventArgs)
    {
        PopupIsOpen = !PopupIsOpen;
    }
}