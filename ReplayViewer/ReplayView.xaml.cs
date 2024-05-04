using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ReplayViewer;

/// <summary>
/// Interaction logic for ReplayView.xaml
/// </summary>
public partial class ReplayView : UserControl
{
    public ReplayView()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        slider.Focus();
    }

    private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }
}
