using Avalonia.Threading;
using Swoq.Infra;
using Swoq.InfraUI.Models;
using Swoq.Interface;
using System.Collections.Immutable;

namespace Swoq.ReplayViewer.ViewModels;

public class GameObservationBuilder(int height, int width, int visibilityRange, string userName)
{
    private static readonly IImmutableDictionary<Inventory, string> InventoryNames = ImmutableDictionary<Inventory, string>.Empty.
        Add(Inventory.None, "-").
        Add(Inventory.KeyRed, "Red key").
        Add(Inventory.KeyGreen, "Green key").
        Add(Inventory.KeyBlue, "Blue key").
        Add(Inventory.Boulder, "Boulder").
        Add(Inventory.Treasure, "Treasure");

    private readonly OverviewBuilder overviewBuilder = new(height, width, visibilityRange);
    private readonly string userName = userName;
    private GameObservation? previous = null;

    public GameObservation BuildNext(ActionRequest? request, State state, Result actionResult, Dispatcher createDispatcher)
    {
        // Clear whole map on new level
        if (previous == null || state.Level != previous.Level)
        {
            overviewBuilder.Reset();
        }

        overviewBuilder.SetLevel(state.Level);
        overviewBuilder.PrepareForNextTimeStep();
        overviewBuilder.AddPlayerState(Convert(state.PlayerState?.Position), state.PlayerState?.Surroundings ?? [], 1);
        overviewBuilder.AddPlayerState(Convert(state.Player2State?.Position), state.Player2State?.Surroundings ?? [], 2);
        var overview = overviewBuilder.CreateOverview();

        var status = state.Finished ? "Finished" : "Active";

        InfraUI.Models.PlayerObservation? player1State = null;
        if (state.PlayerState != null)
        {
            var action1 = request != null
                ? GetPlayerAction(request.HasAction ? request.Action : null)
                : "Start";
            player1State = new InfraUI.Models.PlayerObservation(action1, state.PlayerState.Health, InventoryNames[state.PlayerState.Inventory], state.PlayerState.HasSword);
        }

        InfraUI.Models.PlayerObservation? player2State = null;
        if (state.Player2State != null)
        {
            var action2 = request != null
                ? GetPlayerAction(request.HasAction2 ? request.Action2 : null)
                : "Start";
            player2State = new InfraUI.Models.PlayerObservation(action2, state.Player2State.Health, InventoryNames[state.Player2State.Inventory], state.Player2State.HasSword);
        }

        var gameState = createDispatcher.Invoke(() => new GameObservation(userName, state.Tick, state.Level, status, actionResult.ConvertToString(), overview, player1State, player2State));
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
