using System;
using System.Threading.Tasks;

namespace MusicaLibre.Models;

public interface ISelectVirtualizableItems
{
    public event EventHandler<SelectedItemChangedEventArgs>? SelectionChanged;
    public event EventHandler? SortOrderChanged;
    public event EventHandler<int>? ScrollToIndexRequested;

    public int GetSelectedIndex();
}

public interface IVirtualizableItem
{
    public bool IsSelected {get;set;}
    public bool IsFirst { get; }
    public bool IsPrepared { get; }
    public void OnPrepared();
    public void OnCleared();
}

public class SelectedItemChangedEventArgs : EventArgs
{
    public ISelectVirtualizableItems Sender { get; }
    public IVirtualizableItem? NewItem { get; }
    public IVirtualizableItem? OldItem { get; }
    public SelectedItemChangedEventArgs(ISelectVirtualizableItems sender, IVirtualizableItem? newItem, IVirtualizableItem? oldItem)
    {
        Sender = sender;
        NewItem = newItem;
        OldItem = oldItem;
    }
}
