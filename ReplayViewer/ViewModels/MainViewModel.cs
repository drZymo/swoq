using Swoq.InfraUI.ViewModels;

namespace Swoq.ReplayViewer.ViewModels;

internal class MainViewModel : ViewModelBase
{
    private ReplayViewModel replay;

    public MainViewModel()
    {
        replay = new ReplayViewModel(@"D:\Projects\swoq-stuff\Replays\Quest\Ralph - 6ca62c34-8ffd-4549-ba64-6ddaabcd107a.bin");
    }

    public ReplayViewModel Replay
    {
        get => replay;
        private set
        {
            replay = value;
            OnPropertyChanged();
        }
    }
}
