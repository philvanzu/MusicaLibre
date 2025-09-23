using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicaLibre.ViewModels;

public partial class AlbumUpdatedDialogViewModel: OkCancelViewModel
{
    [ObservableProperty] private bool _selectButtonChecked;
    [ObservableProperty] private bool _createButtonChecked;
    [ObservableProperty] private bool _renameButtonChecked;

    [ObservableProperty] private ObservableCollection<string>? _renames;
    [ObservableProperty] private ObservableCollection<string>? _existings;
    
    [ObservableProperty] int _selectedRenameIndex = -1;
    [ObservableProperty] int _selectedExistingIndex = -1;
    
    [ObservableProperty] string _newAlbumTitle;
    [ObservableProperty] string _newAlbumArtist;
    [ObservableProperty] string _newAlbumYear;
    [ObservableProperty] string _newAlbumFolder;
}