namespace ReplayViewer.ViewModels;

internal class MainViewModel : ViewModelBase
{
    private ReplayViewModel replay;

    public MainViewModel()
    {
        replay = new ReplayViewModel(@"D:\Projects\swoq-stuff\Replays\Training\Ralph\level 9 - d11b7860-2fc2-4d5e-b369-dae05f2c4512.bin");
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
