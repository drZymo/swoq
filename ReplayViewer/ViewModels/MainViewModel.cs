using Microsoft.Win32;
using Swoq.InfraUI.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Swoq.ReplayViewer.ViewModels;

internal class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        LoadCommand = new RelayCommand(Load);
    }

    private ReplayViewModel replay = new();
    public ReplayViewModel Replay
    {
        get => replay;
        private set
        {
            replay = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoadCommand { get; }

    private string currentFile = "";
    public string CurrentFile
    {
        get => currentFile;
        private set
        {
            if (currentFile != value)
            {
                currentFile = value;
                OnPropertyChanged();
            }
        }
    }

    public void LoadFile(string path)
    {
        Replay = new ReplayViewModel(path);
        CurrentFile = path;
    }

    private void Load(object? param)
    {
        var dialog = new OpenFileDialog
        {
            FileName = CurrentFile,
            DefaultExt = ".bin",
            Filter = "Replay files (.bin)|*.bin",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            Title = "Open replay file",
        };
        var result = dialog.ShowDialog();
        if (result.HasValue && result.Value)
        {
            LoadFile(dialog.FileName);
        }
    }
}
