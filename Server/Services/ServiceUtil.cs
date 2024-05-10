using Swoq.Interface;

namespace Swoq.Server.Services;

internal static class ServiceUtil
{
    public static Result ResultFromException(Exception ex, ILogger logger)
    {
        switch (ex)
        {
            case PlayerAlreadyRegisteredException: return Result.PlayerAlreadyRegistered;
            case UnknownPlayerException: return Result.UnknownPlayer;
            case UnknownGameIdException: return Result.UnknownGameId;
            case LevelNotAvailableException: return Result.LevelNotAvailable;
            case MoveNotAllowedException: return Result.MoveNotAllowed;
            case UseNotAllowedException: return Result.UseNotAllowed;
            case UnknownActionException: return Result.UnknownAction;
            case UnknownDirectionException: return Result.UnknownDirection;
            case GameFinishedException: return Result.GameFinished;
            case Player1NotPresentException: return Result.Player1NotPresent;
            case Player2NotPresentException: return Result.Player2NotPresent;
            case InventoryEmptyException: return Result.InventoryEmpty;
            case InventoryFullException: return Result.InventoryFull;
            case NoSwordException: return Result.NoSword;
            case Player1DiedException: return Result.Player1Died;
            case Player2DiedException: return Result.Player2Died;
            case QuestQueuedException: return Result.QuestQueued;
            case NoProgressException: return Result.NoProgress;
        }
        logger.LogError(ex, "Internal error");
        return Result.InternalError;
    }

    public static State Convert(this GameState gameState)
    {
        return new State
        {
            Tick = gameState.Tick,
            Level = gameState.Level,
            Finished = gameState.Finished,
            Player1 = gameState.Player1?.Convert(),
            Player2 = gameState.Player2?.Convert(),
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
