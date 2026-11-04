using Avalonia.Controls;
using Avalonia.Media;
using Jadwal.Application.Services;
using Jadwal.UI.ViewModels;

namespace Jadwal.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ThemeService.FontScaleChanged += SetFontScale;
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
        MainViewControl.DataContext = viewModel;
    }

    public void SetFontScale(double scale)
    {
        if (scale <= 1.001)
        {
            RootLayoutTransform.LayoutTransform = null;
        }
        else
        {
            RootLayoutTransform.LayoutTransform = new ScaleTransform(scale, scale);
        }
    }
}