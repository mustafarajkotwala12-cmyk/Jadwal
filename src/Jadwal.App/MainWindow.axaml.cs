using Avalonia.Controls;
using Jadwal.UI.ViewModels;

namespace Jadwal.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
        MainViewControl.DataContext = viewModel;
    }
}