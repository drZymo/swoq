using Swoq.InfraUI.ViewModels;

namespace Swoq.Dashboard.ViewModels;

internal class MainViewModel : ViewModelBase, IDisposable
{
    public void Dispose()
    {
        GameObserver.Dispose();
    }

    public GameObserverViewModel GameObserver { get; } = new();

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
