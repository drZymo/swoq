using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Swoq.InfraUI.ViewModels;
using System.Windows.Input;

namespace Swoq.ReplayViewer.ViewModels;

internal class MainViewModel : ViewModelBase, IDisposable
{
    public MainViewModel()
    {
        LoadCommand = new RelayCommand(Load);

        // Load file given at command line
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            Task.Run(() => LoadFile(args[1]));
        }
    }

    public void Dispose()
    {
        Replay.Dispose();
    }

    private ReplayViewModel replay = new();
    public ReplayViewModel Replay
    {
        get => replay;
        private set
        {
            replay.Dispose();
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

    private async void Load(object? param)
    {
        // TODO: Proper way of getting StorageProvider (inject?)
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider is not { } provider)
        {
            throw new InvalidOperationException("Missing StorageProvider instance.");
        }

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open replay file",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Replay file") { Patterns = ["*.swoq"] }],
        });

        if (files.Count > 0)
        {
            var localPath = files[0].TryGetLocalPath();
            if (localPath != null)
            {
                LoadFile(localPath);
            }
        }
    }
}
