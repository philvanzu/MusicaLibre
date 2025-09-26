using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MusicaLibre.ViewModels;
using MusicaLibre.ViewModels;

namespace MusicaLibre.Views;

public partial class ProgressDialog : WindowBase
{
    public ProgressDialog() : this(null) { }
    public ProgressDialog(ProgressDialogViewModel? vm)
    {
        InitializeComponent();
        DataContext = vm;
        this.Opened += ((sender, args) =>
        {
            if (DataContext is ProgressDialogViewModel vm)
                vm.NotifyDialogShown();
        });
    }
}