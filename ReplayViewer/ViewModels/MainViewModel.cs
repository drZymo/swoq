using Swoq.InfraUI.ViewModels;

namespace Swoq.ReplayViewer.ViewModels;

internal class MainViewModel : ViewModelBase
{
    private ReplayViewModel replay;

    public MainViewModel()
    {
        replay = new ReplayViewModel(@"D:\Projects\swoq-stuff\Replays\Quest\Second - 2883fb38-b1d1-4883-a0b4-19f3c76aafc8.bin");
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
