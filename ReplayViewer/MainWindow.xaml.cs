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

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel main) return;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
         
        string[]? paths = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (paths == null || paths.Length < 1) return;

        var path = paths[0];
        main.LoadFile(path);
    }
}