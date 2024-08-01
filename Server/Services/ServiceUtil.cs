using Swoq.Interface;

namespace Swoq.Server.Services;

internal static class ServiceUtil
{
    public static Result ResultFromException(Exception ex, ILogger logger)
    {
        switch (ex)
        {
            case UnknownPlayerException: return Result.UnknownPlayer;
            case UnknownGameIdException: return Result.UnknownGameId;
            case PlayerLevelTooLowException: return Result.PlayerLevelTooLow;
            case MoveNotAllowedException: return Result.MoveNotAllowed;
            case UseNotAllowedException: return Result.UseNotAllowed;
            case UnknownActionException: return Result.UnknownAction;
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
            case GameTimeoutException: return Result.GameTimeout;
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
}
