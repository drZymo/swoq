using Viewer.ViewModels;

namespace Viewer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void Window_Closing(object _, System.ComponentModel.CancelEventArgs _)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Dispose();
        }
        DataContext = null;
    }
}