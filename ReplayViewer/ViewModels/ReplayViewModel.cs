using Swoq.InfraUI.Models;
using Swoq.InfraUI.ViewModels;
using Swoq.Interface;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;

namespace Swoq.ReplayViewer.ViewModels;

internal class ReplayViewModel : ViewModelBase
{
    private static readonly TimeSpan FrameDelay = TimeSpan.FromMilliseconds(100);
    private static readonly string[] InventoryNames = ["-", "Red key", "Green key", "Blue key"];

    public ReplayViewModel(string path)
    {
        using var file = File.OpenRead(path);

        var startRequest = StartRequest.Parser.ParseDelimitedFrom(file);
        var startResponse = StartResponse.Parser.ParseDelimitedFrom(file);

        var mapBuilder = new MapBuilder(startResponse.Height, startResponse.Width, startResponse.VisibilityRange);

        AddGameState(ref gameStates, mapBuilder, null, startResponse.State);

        while (file.Position < file.Length)
        {
            var request = ActionRequest.Parser.ParseDelimitedFrom(file);
            var response = ActionResponse.Parser.ParseDelimitedFrom(file);

            AddGameState(ref gameStates, mapBuilder, request, response.State);
        }

        PlayPauseCommand = new RelayCommand(PlayPause);

        OnTickChanged();
    }

    private readonly IImmutableList<GameState> gameStates = ImmutableList<GameState>.Empty;

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

    private DispatcherTimer? timer = null;


    private static void AddGameState(ref IImmutableList<GameState> gameStates, MapBuilder mapBuilder, ActionRequest? request, State state)
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

        var gameState = new GameState("Unknown player", state.Tick, state.Level, status, map, player1State, player2State);
        gameStates = gameStates.Add(gameState);
    }

    private static string GetPlayerAction(Swoq.Interface.Action? action, Swoq.Interface.Direction? direction)
    {
        if (!action.HasValue) return "None";

        var playerAction = new StringBuilder();

        playerAction.Append(action.Value switch
        {
            Interface.Action.Use => "Use",
            Interface.Action.Move => "Move",
            _ => "Unknown",
        });
        if (direction.HasValue)
        {
            playerAction.Append(' ');
            playerAction.Append(direction.Value switch
            {
                Interface.Direction.North => "North",
                Interface.Direction.East => "East",
                Interface.Direction.South => "South",
                Interface.Direction.West => "West",
                _ => "Unknown",
            });
        }
        return playerAction.ToString();
    }

    private void OnTickChanged()
    {
        var gameState = gameStates[tick];
        Current.SetGameState(gameState);
    }

    private void PlayPause(object? _)
    {
        if (timer == null)
        {
            timer = new DispatcherTimer(FrameDelay, DispatcherPriority.Normal, OnTimer, Dispatcher.CurrentDispatcher);
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
