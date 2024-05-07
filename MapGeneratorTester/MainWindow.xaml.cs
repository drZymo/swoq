using Swoq.MapGeneratorTester.ViewModels;
using System.Windows;

namespace Swoq.MapGeneratorTester;

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