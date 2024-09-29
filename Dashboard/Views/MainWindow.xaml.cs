using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Swoq.Dashboard.ViewModels;

namespace Swoq.Dashboard.Views;

public partial class MainWindow : Window
{
    private void Window_Closed(object sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable) disposable.Dispose();
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            if (WindowState != WindowState.FullScreen)
            {
                WindowState = WindowState.FullScreen;
            }
            else
            {
                WindowState = WindowState.Normal;
            }
        }
    }

    private void OnMaximizeClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mvm)
        {
            mvm.Maximize = !mvm.Maximize;
        }
    }
}