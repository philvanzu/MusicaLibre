using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using Avalonia.Controls;
using MusicaLibre.Models;
using MusicaLibre.Services;
using MusicaLibre.Views;

namespace MusicaLibre.ViewModels;

public partial class OrderingEditorViewModel:ViewModelBase
{
    public LibraryViewModel Library { get; set; }
    [ObservableProperty] ObservableCollection<OrderingViewModel> _orderings;
    [ObservableProperty] private OrderingViewModel? _selectedItem;
    Window _window;
    public OrderingEditorViewModel(LibraryViewModel library, Window window)
    {
        _window = window;
        Library = library;
        _orderings = new(
            library.Settings.CustomOrderings.Select(x=> new OrderingViewModel(x, this)));

        _window.Closing += OnWindowClosing;
    }



    [RelayCommand]
    private void Create()
    {
        var ordering = OrderingViewModel.Create(this);
        Orderings.Add(ordering);
        SelectedItem = ordering;
    }

    public void Refresh()
    {
        SelectedItem = null;
        Orderings.Clear();
        Orderings = new ObservableCollection<OrderingViewModel>(
            Library.Settings.CustomOrderings.Select(x=>new OrderingViewModel(x, this)));
    }
    public void Save()
    {
        Library.Settings.Save(Library.Database);
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        
    }

}

public partial class OrderingViewModel : ViewModelBase
{
    
    public string Name
    {
        get =>_model.Name; 
        set => _model.Name = value;
    }

    public bool IsDefault => _model.IsDefault;
    [ObservableProperty] private CustomOrdering _model;
    [ObservableProperty] private ObservableCollection<OrderingStepViewModel> _steps;
    [ObservableProperty] private OrderingStepViewModel _tracksStep;
    
    public OrderingEditorViewModel Parent { get; init; }
    public OrderingViewModel(CustomOrdering model, OrderingEditorViewModel parent)
    {
        Parent = parent;
        _model = model;
        _steps = new ObservableCollection<OrderingStepViewModel>(
            model.Steps.Select(x => new OrderingStepViewModel(x, this, IsDefault)));
        _tracksStep = new OrderingStepViewModel(model.TracksStep, this, IsDefault);
    }

    public static OrderingViewModel Create( OrderingEditorViewModel parent)
    {
        var model = new CustomOrdering()
        {
            Name = "New Custom Ordering",
            Steps = new List<OrderingStep>(),
        };
        return new OrderingViewModel(model, parent);
    }
    [RelayCommand] void Reset()=>Steps.Clear();
    [RelayCommand] void Remove()
    {
        var exists = Parent.Library.Settings.CustomOrderings.FirstOrDefault(x=>x.Name == _model.Name);
        if (exists != null) Parent.Library.Settings.CustomOrderings.Remove(exists);
        Parent.Save();
        Parent.Refresh();
    }
    [RelayCommand] void AddStep()=>Steps.Add(new OrderingStepViewModel(new OrderingStep(), this));
    [RelayCommand] void Save()
    {
        if (Model.IsDefault) return;
        
        List<OrderingStep> steps = new List<OrderingStep>();
        foreach (var stepvm in Steps)
            steps.Add(stepvm.GetModel());

        Model.Steps = steps;
        Model.TracksStep = TracksStep.GetModel();
        
        var exists = Parent.Library.Settings.CustomOrderings.FirstOrDefault(x=>x.Name == _model.Name);
        if (exists != null) Parent.Library.Settings.CustomOrderings.Remove(exists);
        Parent.Library.Settings.CustomOrderings.Add(Model);
        Parent.Save();
        Parent.Refresh();
    }

    [RelayCommand] void RemoveTracksStep()
    {
        if(TracksStep?.SortKeys.Count > 0)
            TracksStep?.SortKeys.Remove(TracksStep.SortKeys.Last());
    }
    [RelayCommand] void CreateTracksStep()
    {
        TracksStep?.SortKeys.Add(TracksStep.Model.DefaultKey);
    }
}

public partial class OrderingStepViewModel : ViewModelBase
{
    public bool IsDefault { get; init; }

    public OrderGroupingType Type
    {
        get=>_model.Type;
        set => _model.Type = value;
    }
    [ObservableProperty] OrderingStep _model;
    [ObservableProperty] private OrderGroupingType _groupingType;
    [ObservableProperty] private ObservableCollection<SortingKey> _sortKeys;
    [ObservableProperty] private ObservableCollection<SortingKey<TrackSortKeys>> _sortTracksKeys;
    public static string[] GroupingOptions => EnumUtils.GetDisplayNames<OrderGroupingType>();
    public OrderingViewModel Parent { get; init; }
    public OrderingStepViewModel(OrderingStep model, OrderingViewModel parent, bool isDefault = false)
    {
        Parent = parent;
        IsDefault = isDefault;
        _model=model;
        _sortKeys=new ObservableCollection<SortingKey>(model.SortingKeys);
        _sortTracksKeys =  new ObservableCollection<SortingKey<TrackSortKeys>>(model.TracksSortingKeys);
    }

    public OrderingStep GetModel()
    {
        var model = new OrderingStep()
        {
            Type = _model.Type,
            SortingKeys = SortKeys.ToList(),
            TracksSortingKeys = SortTracksKeys.ToList(),
        };
        return model;
    }

    [RelayCommand] void MoveUp()
    {
        var index = Parent.Steps.IndexOf(this);
        if (index > 0)
        {
            (Parent.Steps[index - 1], Parent.Steps[index]) = (Parent.Steps[index], Parent.Steps[index - 1]);
        }
    }

    public bool CanMoveUp => Parent.Steps.IndexOf(this) > 0;
    [RelayCommand]
    void MoveDown()
    {
        var index = Parent.Steps.IndexOf(this);
        if (index >= 0 && index < Parent.Steps.Count - 1)
        {
            (Parent.Steps[index], Parent.Steps[index + 1]) = (Parent.Steps[index + 1], Parent.Steps[index]);
        }
    }
    public bool CanMoveDown => Parent.Steps.IndexOf(this) < Parent.Steps.Count - 1;

    [RelayCommand]
    void Delete()
    {
        if(Parent.Steps.Count > 0)
            Parent.Steps.Remove(this);
    }
    [RelayCommand]void AddKey()=> SortKeys.Add(_model.DefaultKey);

    [RelayCommand]
    void RemoveKey()
    {
        if (SortKeys.Count>0)
            SortKeys.Remove(SortKeys.Last());   
    }

    [RelayCommand]
    void CreateTrackStep()
    {
        SortTracksKeys.Add(new SortingKey<TrackSortKeys>(TrackSortKeys.Title,  true));
    }

    [RelayCommand]
    void RemoveTrackStep()
    {
        if(SortTracksKeys.Count > 0)
            SortTracksKeys.Remove(SortTracksKeys.Last());
    }
}

