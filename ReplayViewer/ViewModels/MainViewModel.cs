using Swoq.InfraUI.ViewModels;

namespace Swoq.ReplayViewer.ViewModels;

internal class MainViewModel : ViewModelBase
{
    private ReplayViewModel replay;

    public MainViewModel()
    {
        replay = new ReplayViewModel(@"D:\Projects\swoq-stuff\Replays\Quest\Ralph - 7f9bcac5-70d6-4db5-a5fd-d0775dc94d0f.bin");
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
