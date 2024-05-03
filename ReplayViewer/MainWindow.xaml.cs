using ReplayViewer.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace ReplayViewer;

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

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        slider.Focus();
    }

    private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }
}