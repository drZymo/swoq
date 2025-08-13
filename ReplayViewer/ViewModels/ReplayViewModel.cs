using Avalonia.Threading;
using Swoq.InfraUI.Models;
using Swoq.InfraUI.ViewModels;
using Swoq.Interface;
using Swoq.ReplayViewer.Util;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Windows.Input;

namespace Swoq.ReplayViewer.ViewModels;

internal class ReplayViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan FrameDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan TailModeFrameDelay = TimeSpan.FromMilliseconds(20);

    private readonly IErrorReporter reporter;
    private bool tailMode = false;
    private DispatcherTimer? playTimer = null;
    private DispatcherTimer? tailTimer = null;

    private IImmutableList<GameObservation> gameStates = ImmutableList<GameObservation>.Empty;

    public ReplayViewModel(IErrorReporter reporter)
    {
        this.reporter = reporter;
        PlayPauseCommand = new RelayCommand(PlayPause);
    }

    public ReplayViewModel(IErrorReporter reporter, string path, bool tailMode = false) : this(reporter)
    {
        IsLoading = !tailMode;
        this.tailMode = tailMode;
        if (tailMode)
        {
            tailTimer = new DispatcherTimer(TailModeFrameDelay, DispatcherPriority.Normal, OnTailTimer);
        }
        Task.Run(() => Load(path));
    }

    public void Dispose()
    {
        Current.Dispose();
    }

    private bool isLoading = false;
    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (isLoading != value)
            {
                isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    public event Action? Loaded;

    private int tick = 0;
    public int Tick
    {
        get => tick;
        set
        {
            if (tick != value)
            {
                if (MaxTick >= 0)
                {
                    tick = Math.Clamp(value, 0, MaxTick);
                }
                else
                {
                    tick = 0;
                }
                OnPropertyChanged();
                OnTickChanged();
            }
        }
    }

    public GameObservationViewModel Current { get; } = new();

    public int MaxTick => gameStates.Count - 1;

    public ICommand PlayPauseCommand { get; }

    private void Load(string path)
    {
        try
        {
            var sw = Stopwatch.StartNew();

            using Stream file = tailMode
                ? new TailStream(path)
                : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var startRequest = StartRequest.Parser.ParseDelimitedFrom(file);
            var startResponse = StartResponse.Parser.ParseDelimitedFrom(file);

            var builder = new GameObservationBuilder(startResponse.GameId, startResponse.MapHeight, startResponse.MapWidth, startResponse.VisibilityRange, startRequest.UserName);

            var gameState = builder.BuildNext(null, startResponse.State, startResponse.Result, Dispatcher.UIThread);
            gameStates = gameStates.Add(gameState);

            do
            {
                if (!tailMode && file.Position >= file.Length)
                {
                    // End of file reached (e.g. bot crashed)
                    break;
                }
                var request = ActRequest.Parser.ParseDelimitedFrom(file);
                var response = ActResponse.Parser.ParseDelimitedFrom(file);

                gameState = builder.BuildNext(request, response.State, response.Result, Dispatcher.UIThread);
                gameStates = gameStates.Add(gameState);
            } while (gameState.Status == GameStatus.Active);
            sw.Stop();

            Debug.WriteLine($"Loaded {path} in {sw.Elapsed.TotalSeconds:F1} seconds, {gameStates.Count - 1} ticks.");
        }
        catch (Exception error)
        {
            // Keep game states loaded so far
            Console.WriteLine($"Failed to load replay file: {path}\n{error}");
            if (gameStates.Count == 0)
            {
                reporter.ReportError($"Failed to load replay file: {path}\n\n{error}");
            }
        }
        finally
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                OnPropertyChanged(nameof(MaxTick));
                if (tailMode)
                {
                    tailMode = false;
                    // Jump to the end if the user didn't pause before.
                    Tick = gameStates.Count - 1;
                    tailTimer?.Stop();
                    tailTimer = null;
                }
                else
                {
                    OnTickChanged();
                }
                IsLoading = false;
                Loaded?.Invoke();
            });
        }
    }

    private void OnTickChanged()
    {
        if (gameStates.Count == 0)
        {
            Current.Reset();
        }
        else
        {
            var gameState = gameStates[tick];
            Current.SetGameObservation(gameState);
        }
    }

    private void PlayPause(object? _)
    {
        if (tailMode)
        {
            // Stop tail mode when 'manually' pressing play/pause (but keep parsing in background)
            tailMode = false;
        }
        else if (playTimer == null)
        {
            playTimer = new DispatcherTimer(FrameDelay, DispatcherPriority.Normal, OnPlayTimer);
        }
        else
        {
            playTimer.Stop();
            playTimer = null;
        }
    }

    private void OnPlayTimer(object? sender, EventArgs e)
    {
        if (Tick >= MaxTick)
        {
            // Reached the end of the replay, stop playing
            PlayPause(null);
        }
        else
        {
            Tick++;
        }
    }

    private void OnTailTimer(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(MaxTick));
        if (tailMode)
        {
            Tick = gameStates.Count - 1;
        }
    }
}
