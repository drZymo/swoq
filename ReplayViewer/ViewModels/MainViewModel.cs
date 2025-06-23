using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Swoq.InfraUI.ViewModels;
using Swoq.ReplayViewer.Util;

namespace Swoq.ReplayViewer.ViewModels;

internal class MainViewModel : ViewModelBase, IDisposable
{
    public MainViewModel()
    {
        LoadCommand = new RelayCommand(Load);

        // Load file given at command line, or start watch mode
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && (args[1] == "--watch" || args[1] == "-w"))
        {
            if (args.Length <= 2)
            {
                throw new ArgumentException("Missing folder path for --watch argument.");
            }
            StartWatchingFolder(Path.GetFullPath(args[2]));
        }
        else if (args.Length > 1)
        {
            Task.Run(() => LoadFile(args[1]));
        }
    }

    private IDisposable? fileWatcherSubscription;

    public void Dispose()
    {
        fileWatcherSubscription?.Dispose();
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

    public void LoadFile(string path, bool tailMode = false)
    {
        var mode = tailMode ? "Tail" : "Load";
        Console.WriteLine($"{mode} file: {path}");
        Replay = new ReplayViewModel(path, tailMode);
        CurrentFile = path;
    }

    private void StartWatchingFolder(string folderPath)
    {
        if (SynchronizationContext.Current == null)
        {
            throw new InvalidOperationException("This method must be called on the UI thread.");
        }
        Console.WriteLine($"Watching folder: {folderPath}");
        fileWatcherSubscription = FileWatcherObservable.WatchSwoqFiles(folderPath)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(
                eventArgs => LoadFile(eventArgs.FullPath, true),
                onError: e => Console.WriteLine($"Can't watch folder {folderPath}: {e.Message}")
            );
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
            fileWatcherSubscription?.Dispose();
            fileWatcherSubscription = null;

            var localPath = files[0].TryGetLocalPath();
            if (localPath != null)
            {
                LoadFile(localPath);
            }
        }
    }
}
