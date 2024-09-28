using Swoq.InfraUI.ViewModels;

namespace Swoq.Dashboard.ViewModels;

internal class MainViewModel : ViewModelBase, IDisposable
{
    public void Dispose()
    {
        GameStateMonitor.Dispose();
    }

    public GameStateMonitorViewModel GameStateMonitor { get; } = new();
}
