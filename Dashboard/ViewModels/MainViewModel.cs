using Swoq.InfraUI.ViewModels;

namespace Swoq.Dashboard.ViewModels;

internal class MainViewModel : ViewModelBase, IDisposable
{
    public void Dispose()
    {
        GameStateMonitor.Dispose();
    }

    public GameStateMonitorViewModel GameStateMonitor { get; } = new();

    private bool maximize = false;
    public bool Maximize
    {
        get => maximize;
        set
        {
            if (maximize != value)
            {
                maximize = value;
                OnPropertyChanged();
            }
        }
    }
}
