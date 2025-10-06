using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicaLibre.Services;

namespace MusicaLibre.ViewModels;

public partial class ListPickViewModel: OkCancelViewModel
{
    [ObservableProperty] private ObservableCollection<string> _list;
    [ObservableProperty] int _selectedIndex;
}