using Avalonia.Threading;
using Swoq.InfraUI.Models;
using Swoq.InfraUI.ViewModels;
using Swoq.Interface;
using System.Collections.Immutable;
using System.Text;
using System.Windows.Input;

namespace Swoq.ReplayViewer.ViewModels;

internal class ReplayViewModel : ViewModelBase
{
    private static readonly TimeSpan FrameDelay = TimeSpan.FromMilliseconds(100);
    private static readonly string[] InventoryNames = ["-", "Red key", "Green key", "Blue key"];

    private DispatcherTimer? timer = null;

    private IImmutableList<GameState> gameStates = ImmutableList<GameState>.Empty;

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

    public GameStateViewModel Current { get; } = new GameStateViewModel();

    public int MaxTick => gameStates.Count - 1;

    public ICommand PlayPauseCommand { get; }


    private void Load(string path)
    {
        try
        {
            using var file = File.OpenRead(path);

            var header = ReplayHeader.Parser.ParseDelimitedFrom(file);
            var startRequest = StartRequest.Parser.ParseDelimitedFrom(file);
            var startResponse = StartResponse.Parser.ParseDelimitedFrom(file);

            var mapBuilder = new MapBuilder(startResponse.Height, startResponse.Width, startResponse.VisibilityRange);

            AddGameState(header.PlayerName, mapBuilder, null, startResponse.State, startResponse.Result);

            while (file.Position < file.Length)
            {
                var request = ActionRequest.Parser.ParseDelimitedFrom(file);
                var response = ActionResponse.Parser.ParseDelimitedFrom(file);

                AddGameState(header.PlayerName, mapBuilder, request, response.State, response.Result);
            }
        }
        catch
        {
            gameStates = ImmutableList<GameState>.Empty;
            // TODO: MessageBox.Show($"Failed to load replay file\n\n{path}");
        }
        finally
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                Tick = 0;
                OnPropertyChanged(nameof(MaxTick));
                IsLoading = false;
                OnTickChanged();
            });
        }
    }

    private void AddGameState(string playerName, MapBuilder mapBuilder, ActionRequest? request, State state, Result actionResult)
    {
        // Clear whole map on new level
        if (gameStates.Count > 0 && state.Level != gameStates[^1].Level)
        {
            mapBuilder.Reset();
        }

        mapBuilder.PrepareForNextTimeStep();
        mapBuilder.AddPlayerState(state.Player1, 1);
        mapBuilder.AddPlayerState(state.Player2, 2);
        var map = mapBuilder.CreateMap();

        var status = state.Finished ? "Finished" : "Active";

        InfraUI.Models.PlayerState? player1State = null;
        if (state.Player1 != null)
        {
            var action1 = request != null
                ? GetPlayerAction(request.HasAction1 ? request.Action1 : null, request.HasDirection1 ? request.Direction1 : null)
                : "Start";
            player1State = new InfraUI.Models.PlayerState(action1, state.Player1.Health, InventoryNames[state.Player1.Inventory], state.Player1.HasSword);
        }

        InfraUI.Models.PlayerState? player2State = null;
        if (state.Player2 != null)
        {
            var action2 = request != null
                ? GetPlayerAction(request.HasAction2 ? request.Action2 : null, request.HasDirection2 ? request.Direction2 : null)
                : "Start";
            player2State = new InfraUI.Models.PlayerState(action2, state.Player2.Health, InventoryNames[state.Player2.Inventory], state.Player2.HasSword);
        }

        var gameState = CreateOnUI(() => new GameState(playerName, state.Tick, state.Level, status, actionResult.ConvertToString(), map, player1State, player2State)) ?? throw new InvalidOperationException();
        gameStates = gameStates.Add(gameState);
    }

    private T? CreateOnUI<T>(Func<T> func)
    {
        T? value = default;
        Dispatcher.UIThread.Invoke(() => { value = func(); });
        return value;
    }

    private static string GetPlayerAction(Swoq.Interface.Action? action, Swoq.Interface.Direction? direction)
    {
        if (!action.HasValue) return "None";

        var playerAction = new StringBuilder();

        playerAction.Append(action.Value.ConvertToString());
        if (direction.HasValue)
        {
            playerAction.Append(' ');
            playerAction.Append(direction.Value.ConvertToString());
        }
        return playerAction.ToString();
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
            Current.SetGameState(gameState);
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
