using Avalonia.Threading;
using Swoq.InfraUI.Models;
using Swoq.InfraUI.ViewModels;
using Swoq.Interface;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Windows.Input;

namespace Swoq.ReplayViewer.ViewModels;

internal class ReplayViewModel : ViewModelBase
{
    private static readonly TimeSpan FrameDelay = TimeSpan.FromMilliseconds(100);

    private DispatcherTimer? timer = null;

    private IImmutableList<GameObservation> gameStates = ImmutableList<GameObservation>.Empty;

    public ReplayViewModel()
    {
        PlayPauseCommand = new RelayCommand(PlayPause);
    }

    public ReplayViewModel(string path) : this()
    {
        IsLoading = true;
        Task.Run(() => Load(path));
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


    private int tick = 0;
    public int Tick
    {
        get => tick;
        set
        {
            if (tick != value)
            {
                tick = Math.Clamp(value, 0, MaxTick);
                OnPropertyChanged();
                OnTickChanged();
            }
        }
    }

    public GameObservationViewModel Current { get; } = new GameObservationViewModel();

    public int MaxTick => gameStates.Count - 1;

    public ICommand PlayPauseCommand { get; }

    private void Load(string path)
    {
        gameStates = ImmutableList<GameObservation>.Empty;

        try
        {
            var sw = Stopwatch.StartNew();
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var header = ReplayHeader.Parser.ParseDelimitedFrom(file);
            var startRequest = StartRequest.Parser.ParseDelimitedFrom(file);
            var startResponse = StartResponse.Parser.ParseDelimitedFrom(file);

            var builder = new GameObservationBuilder(startResponse.GameId, startResponse.Height, startResponse.Width, startResponse.VisibilityRange, header.UserName);

            var gameState = builder.BuildNext(null, startResponse.State, startResponse.Result, Dispatcher.UIThread);
            gameStates = gameStates.Add(gameState);

            while (file.Position < file.Length)
            {
                var request = ActionRequest.Parser.ParseDelimitedFrom(file);
                var response = ActionResponse.Parser.ParseDelimitedFrom(file);

                gameState = builder.BuildNext(request, response.State, response.Result, Dispatcher.UIThread);
                gameStates = gameStates.Add(gameState);
            }

            sw.Stop();

            Debug.WriteLine($"Loaded {file.Name} in {sw.Elapsed.TotalSeconds:F1} seconds.");
        }
        catch
        {
            // TODO: MessageBox.Show($"Failed to load replay file\n\n{path}");
            // Keep game states loaded so far
        }
        finally
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                Tick = 0;
                OnPropertyChanged(nameof(MaxTick));
                OnTickChanged();
                IsLoading = false;
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
        if (timer == null)
        {
            timer = new DispatcherTimer(FrameDelay, DispatcherPriority.Normal, OnTimer);
        }
        else
        {
            timer.Stop();
            timer = null;
        }
    }

    private void OnTimer(object? sender, EventArgs e)
    {
        Tick++;
    }
}
