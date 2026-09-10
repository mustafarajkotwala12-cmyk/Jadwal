using Avalonia.Controls;
using Avalonia.Interactivity;
using Jadwal.UI.ViewModels;

namespace Jadwal.UI.Controls;

public partial class ClassCardControl : UserControl
{
    public ClassCardControl()
    {
        InitializeComponent();
    }

    private void OnFlipClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ClassCardItemViewModel vm)
        {
            vm.ToggleFlip();
        }
    }
}
