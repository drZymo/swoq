using Avalonia.Threading;
using Swoq.Infra;
using Swoq.Interface;
using System.Collections.Immutable;

namespace Swoq.InfraUI.Models;

public class GameObservationBuilder(string id, int height, int width, int visibilityRange, string userName)
{
    private readonly OverviewBuilder overviewBuilder = new(height, width, visibilityRange);
    private readonly string userName = userName;
    private GameObservation? previous = null;
    private bool hasSwordPickup = false;
    private bool hasEnemies = false;

    private readonly int visibilityDimension = visibilityRange * 2 + 1;
    private readonly ImmutableArray<bool> visibility = Enumerable.Repeat(true, (visibilityRange * 2 + 1) * (visibilityRange * 2 + 1)).ToImmutableArray();

    public string GameId { get; } = id;

    public GameObservation BuildNext(ActionRequest? request, State state, StartResult actionResult, Dispatcher createDispatcher)
    {
        return BuildNext(request, state, actionResult.ConvertToString(), createDispatcher);
    }
    public GameObservation BuildNext(ActionRequest? request, State state, ActResult actionResult, Dispatcher createDispatcher)
    {
        return BuildNext(request, state, actionResult.ConvertToString(), createDispatcher);
    }

    public GameObservation BuildNext(ActionRequest? request, State state, string actionResult, Dispatcher createDispatcher)
    {
        // Clear whole map on new level
        if (previous == null || state.Level != previous.Level)
        {
            overviewBuilder.Reset();
        }

        overviewBuilder.PrepareForNextTimeStep();
        overviewBuilder.AddPlayerState(Convert(state.PlayerState?.Position), state.PlayerState?.Surroundings ?? [], 1);
        overviewBuilder.AddPlayerState(Convert(state.Player2State?.Position), state.Player2State?.Surroundings ?? [], 2);
        var overview = overviewBuilder.CreateOverview();

        if (state.PlayerState?.Surroundings?.Any(t => t == Tile.Sword) ?? false)
        {
            hasSwordPickup = true;
        }
        if (state.PlayerState?.Surroundings?.Any(t => t == Tile.Enemy || t == Tile.Boss) ?? false)
        {
            hasEnemies = true;
        }

        PlayerObservation? player1State = null;
        if (state.PlayerState != null)
        {
            var action1 = request != null
                ? GetPlayerAction(request.HasAction ? request.Action : null)
                : "Start";
            var surroundings = state.PlayerState.Surroundings.Count == visibilityDimension * visibilityDimension ?
                new TileMap(visibilityDimension, visibilityDimension, [.. state.PlayerState.Surroundings], visibility) :
                TileMap.Empty;
            player1State = new PlayerObservation(action1, state.PlayerState.Health, state.PlayerState.Inventory, state.PlayerState.HasSword, surroundings);
        }

        PlayerObservation? player2State = null;
        if (state.Player2State != null)
        {
            var action2 = request != null
                ? GetPlayerAction(request.HasAction2 ? request.Action2 : null)
                : "Start";
            var surroundings = state.Player2State.Surroundings.Count == visibilityDimension * visibilityDimension ?
                new TileMap(visibilityDimension, visibilityDimension, [.. state.Player2State.Surroundings], visibility) :
                TileMap.Empty;
            player2State = new PlayerObservation(action2, state.Player2State.Health, state.Player2State.Inventory, state.Player2State.HasSword, surroundings);
        }

        var gameState = createDispatcher.Invoke(() => new GameObservation(
            userName,
            state.Tick,
            state.Level,
            state.Status,
            actionResult,
            hasEnemies,
            hasSwordPickup,
            overview,
            player1State,
            player2State));
        previous = gameState;
        return gameState;
    }

    private static string GetPlayerAction(DirectedAction? action)
    {
        if (!action.HasValue) return "None";

        return action.Value.ConvertToString();
    }

    private static (int y, int x) Convert(Interface.Position? position) => position == null ? (-1, -1) : (position.Y, position.X);

    public static bool IsPickup(Tile tile) => tile switch
    {
        Tile.KeyRed => true,
        Tile.KeyGreen => true,
        Tile.KeyBlue => true,
        Tile.Boulder => true,
        Tile.Treasure => true,
        _ => false,
    };
}
