using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Swoq.InfraUI.ViewModels;
using Swoq.ReplayViewer.Util;
using System.Reactive.Linq;
using System.Windows.Input;

namespace Swoq.ReplayViewer.ViewModels;

internal class MainViewModel : ViewModelBase, IDisposable, IErrorReporter
{
    private readonly IStorageProvider storageProvider;

    public MainViewModel(IStorageProvider storageProvider)
    {
        this.storageProvider = storageProvider;

        LoadCommand = new RelayCommand(Load);

        // Start with empty replay
        replay = new(this);

        // Load file given at command line, or start watch mode
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && (args[1] == "--watch" || args[1] == "-w"))
        {
            if (args.Length > 2)
            {
                StartWatchingFolder(Path.GetFullPath(args[2]));
            }
            else
            {
                ReportError("Missing folder path for --watch argument.");
            }
        }
        else if (args.Length > 1)
        {
            LoadFile(args[1]);
        }
    }

    private IDisposable? fileWatcherSubscription;

    public void Dispose()
    {
        fileWatcherSubscription?.Dispose();
        Replay.Dispose();
    }

    private ReplayViewModel replay;
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
        Dispatcher.UIThread.Invoke(() =>
        {
            var mode = tailMode ? "Tail" : "Load";
            Console.WriteLine($"{mode} file: {path}");
            Replay = new ReplayViewModel(this, path, tailMode);
            CurrentFile = path;
        });
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
                onError: e => ReportError($"Can't watch folder {folderPath}.\n\n{e}")
            );
    }

    private async void Load(object? param)
    {
        ClearErrorMessage();

        try
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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
        catch (Exception ex)
        {
            ReportError($"File loading failed.\n\n{ex}");
        }
    }

    private string? errorMessage;

    public string ErrorMessage => errorMessage ?? string.Empty;

    public bool HasErrorMessage => errorMessage != null;

    private void ClearErrorMessage()
    {
        errorMessage = null;
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasErrorMessage));
    }

    public void ReportError(string message)
    {
        Console.WriteLine($"Error: {message}");
        Dispatcher.UIThread.Invoke(() =>
        {
            errorMessage = message;
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(HasErrorMessage));
        });
    }
}
