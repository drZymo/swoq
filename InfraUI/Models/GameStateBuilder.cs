using Avalonia.Threading;
using Swoq.Infra;
using Swoq.InfraUI.Models;
using Swoq.Interface;
using System.Collections.Immutable;

namespace Swoq.ReplayViewer.ViewModels;

public class GameStateBuilder(int height, int width, int visibilityRange, string userName)
{
    private static readonly IImmutableDictionary<Inventory, string> InventoryNames = ImmutableDictionary<Inventory, string>.Empty.
        Add(Inventory.None, "-").
        Add(Inventory.KeyRed, "Red key").
        Add(Inventory.KeyGreen, "Green key").
        Add(Inventory.KeyBlue, "Blue key").
        Add(Inventory.Boulder, "Boulder").
        Add(Inventory.Treasure, "Treasure");

    private readonly MapBuilder mapBuilder = new(height, width, visibilityRange);
    private readonly string userName = userName;
    private GameState? previous = null;

    public GameState BuildNext(ActionRequest? request, State state, Result actionResult, Dispatcher createDispatcher)
    {
        // Clear whole map on new level
        if (previous == null || state.Level != previous.Level)
        {
            mapBuilder.Reset();
        }

        mapBuilder.SetLevel(state.Level);
        mapBuilder.PrepareForNextTimeStep();
        mapBuilder.AddPlayerState(Convert(state.Player1?.Position), state.Player1?.Surroundings ?? [], 1);
        mapBuilder.AddPlayerState(Convert(state.Player2?.Position), state.Player2?.Surroundings ?? [], 2);
        var map = mapBuilder.CreateMap();

        var status = state.Finished ? "Finished" : "Active";

        InfraUI.Models.PlayerState? player1State = null;
        if (state.Player1 != null)
        {
            var action1 = request != null
                ? GetPlayerAction(request.HasAction1 ? request.Action1 : null)
                : "Start";
            player1State = new InfraUI.Models.PlayerState(action1, state.Player1.Health, InventoryNames[state.Player1.Inventory], state.Player1.HasSword);
        }

        InfraUI.Models.PlayerState? player2State = null;
        if (state.Player2 != null)
        {
            var action2 = request != null
                ? GetPlayerAction(request.HasAction2 ? request.Action2 : null)
                : "Start";
            player2State = new InfraUI.Models.PlayerState(action2, state.Player2.Health, InventoryNames[state.Player2.Inventory], state.Player2.HasSword);
        }

        var gameState = createDispatcher.Invoke(() => new GameState(userName, state.Tick, state.Level, status, actionResult.ConvertToString(), map, player1State, player2State));
        previous = gameState;
        return gameState;
    }

    private static string GetPlayerAction(DirectedAction? action)
    {
        if (!action.HasValue) return "None";

        return action.Value.ConvertToString();
    }

    private static (int y, int x) Convert(Position? position) => position == null ? PositionEx.Invalid : (position.Y, position.X);
}
