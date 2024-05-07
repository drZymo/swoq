using Swoq.InfraUI.ViewModels;

namespace Swoq.QuestDashboard.ViewModels;

internal class MainViewModel : ViewModelBase, IDisposable
{
    public MainViewModel()
    {
    }

    public void Dispose()
    {
        GameStateMonitor.Dispose();
    }

    public GameStateMonitorViewModel GameStateMonitor { get; } = new();
}
