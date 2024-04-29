using Swoq.Interface;

namespace Swoq.Server;

internal static class ServiceUtil
{
    public static (Result result, GameState? state) ResultFromException(Exception ex, ILogger logger)
    {
        switch (ex)
        {
            case PlayerAlreadyRegisteredException: return (Result.PlayerAlreadyRegistered, null);
            case UnknownPlayerException: return (Result.UnknownPlayer, null);
            case UnknownGameIdException: return (Result.UnknownGameId, null);
            case LevelNotAvailableException: return (Result.LevelNotAvailable, null);
            case MoveNotAllowedException e: return (Result.MoveNotAllowed, e.State);
            case UseNotAllowedException e: return (Result.UseNotAllowed, e.State);
            case UnknownActionException e: return (Result.UnknownAction, e.State);
            case UnknownDirectionException e: return (Result.UnknownDirection, e.State);
            case GameFinishedException e: return (Result.GameFinished, e.State);
            case Player1NotPresentException e: return (Result.Player1NotPresent, e.State);
            case Player2NotPresentException e: return (Result.Player2NotPresent, e.State);
            case InventoryEmptyException e: return (Result.InventoryEmpty, e.State);
            case InventoryFullException e: return (Result.InventoryFull, e.State);
            case NoSwordException e: return (Result.NoSword, e.State);
            case Player1DiedException e: return (Result.Player1Died, e.State);
            case Player2DiedException e: return (Result.Player2Died, e.State);
            case UnknownQuestIdException e: return (Result.UnknownQuestId, e.State);
        }
        logger.LogError(ex, "Internal error");
        return (Result.InternalError, null);
    }

    public static State Convert(this GameState gameState)
    {
        return new State
        {
            Level = gameState.Level,
            Finished = gameState.Finished,
            Player1 = gameState.Player1 != null ? Convert(gameState.Player1) : null,
            Player2 = gameState.Player2 != null ? Convert(gameState.Player2) : null,
        };
    }

    public static Interface.PlayerState Convert(this PlayerState playerState)
    {
        var state = new Interface.PlayerState
        {
            Position = new Position { X = playerState.Position.x, Y = playerState.Position.y },
            Health = playerState.Health,
            Inventory = playerState.Inventory,
            HasSword = playerState.HasSword,
        };
        state.Surroundings.AddRange(playerState.Surroundings);
        return state;
    }

    public static Action Convert(this Interface.Action action) => action switch
    {
        Interface.Action.Move => Action.Move,
        Interface.Action.Use => Action.Use,
        _ => throw new NotImplementedException(),
    };

    public static Direction Convert(this Interface.Direction direction) => direction switch
    {
        Interface.Direction.North => Direction.North,
        Interface.Direction.East => Direction.East,
        Interface.Direction.South => Direction.South,
        Interface.Direction.West => Direction.West,
        _ => throw new NotImplementedException(),
    };
}
