using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicaLibre.Models;

namespace MusicaLibre.ViewModels;

public partial class TrackViewColumn:ViewModelBase
{
    public record MapItem (TrackSortKeys SortKey, Func<TrackViewModel, string> RowGetter);
    
    public TracksListViewModel Presenter { get; init; }
    public string Key { get; init; } // header text

    [ObservableProperty] bool _isSorting;
    partial void OnIsSortingChanged(bool value)
    {
        if(value) Presenter.SortingColumn = this;
    }

    [ObservableProperty] bool _isAscending=true;
    partial void OnIsAscendingChanged(bool value)
    {
        Presenter.Reverse();
    }

    public bool IsVisible { get; set; }
    public bool IsCentered { get; set; }
    public int ColumnIndex => Presenter.GetColumnIndex(this);
    
    public double Width { get; set; } = double.NaN; // Auto by default
    public TrackSortKeys SortKey { get; init; }
    public Func<TrackViewModel, string> RowGetter { get; init; }
    public Func<TrackViewModel, string> ToolTipGetter { get; set; }
    public TrackViewColumn(string key, TrackSortKeys sortKey, Func<TrackViewModel, string> rowGetter,  
        TracksListViewModel presenter, bool isVisible=true, bool isCentered=false)
    {
        IsCentered = isCentered;
        IsVisible = isVisible;
        Key = key;
        SortKey = sortKey;
        Presenter = presenter;
        RowGetter = rowGetter;
    }
    
    public string GetRowText(TrackViewModel row) => RowGetter.Invoke(row);


}