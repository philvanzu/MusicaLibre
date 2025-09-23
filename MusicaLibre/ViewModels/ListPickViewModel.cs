using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicaLibre.ViewModels;

public partial class ListPickViewModel: OkCancelViewModel
{
    [ObservableProperty] ObservableCollection<string> _list;
    [ObservableProperty] int _selectedIndex;
    
    
}