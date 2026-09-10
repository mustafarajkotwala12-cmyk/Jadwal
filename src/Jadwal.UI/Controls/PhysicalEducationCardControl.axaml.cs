using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Jadwal.UI.ViewModels;

namespace Jadwal.UI.Controls;

public partial class PhysicalEducationCardControl : UserControl
{
    public PhysicalEducationCardControl()
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

    private void OnNewTaskKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ClassCardItemViewModel vm)
        {
            if (vm.AddTaskCommand.CanExecute(null))
            {
                vm.AddTaskCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
