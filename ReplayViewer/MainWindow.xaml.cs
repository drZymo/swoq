using Swoq.ReplayViewer.ViewModels;
using System.Windows;

namespace Swoq.ReplayViewer;

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
}