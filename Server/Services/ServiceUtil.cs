using Swoq.Interface;

namespace Swoq.Server.Services;

internal static class ServiceUtil
{
    public static Result ResultFromException(Exception ex, ILogger logger)
    {
        switch (ex)
        {
            case UnknownUserException: return Result.UnknownUser;
            case UnknownGameIdException: return Result.UnknownGameId;
            case UserLevelTooLowException: return Result.UserLevelTooLow;
            case MoveNotAllowedException: return Result.MoveNotAllowed;
            case UseNotAllowedException: return Result.UseNotAllowed;
            case UnknownActionException: return Result.UnknownAction;
            case GameFinishedException: return Result.GameFinished;
            case Player1NotPresentException: return Result.PlayerNotPresent;
            case Player2NotPresentException: return Result.Player2NotPresent;
            case InventoryEmptyException: return Result.InventoryEmpty;
            case InventoryFullException: return Result.InventoryFull;
            case NoSwordException: return Result.NoSword;
            case QuestQueuedException: return Result.QuestQueued;
            case QuestAlreadyActiveException: return Result.QuestAlreadyActive;
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
            Status = gameState.Status,
            PlayerState = gameState.Player1?.Convert(),
            Player2State = gameState.Player2?.Convert(),
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
