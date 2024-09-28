using Swoq.InfraUI.ViewModels;

namespace Swoq.Dashboard.ViewModels;

internal class MainViewModel : ViewModelBase, IDisposable
{
    public void Dispose()
    {
        GameStateMonitor.Dispose();
        Scores.Dispose();
    }

    public ScoresViewModel Scores { get; } = new();
    public GameStateMonitorViewModel GameStateMonitor { get; } = new();
}
