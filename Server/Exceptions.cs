using Swoq.Interface;

namespace Swoq.Server;

public abstract class SwoqException : Exception;

// Start
public abstract class SwoqStartException(StartResult result) : Exception
{
    public StartResult Result { get; } = result;
}
public class UnknownUserException : SwoqStartException { public UnknownUserException() : base(StartResult.UnknownUser) { } }
public class InvalidLevelException : SwoqStartException { public InvalidLevelException() : base(StartResult.InvalidLevel) { } }
public class QuestQueuedException : SwoqStartException { public QuestQueuedException() : base(StartResult.QuestQueued) { } }
public class QuestAlreadyActiveException : SwoqStartException { public QuestAlreadyActiveException() : base(StartResult.QuestAlreadyActive) { } }

// Act
public abstract class SwoqActException(ActResult result) : Exception
{
    public ActResult Result { get; } = result;
}
public class UnknownGameIdException : SwoqActException { public UnknownGameIdException() : base(ActResult.UnknownGameId) { } }
public class MoveNotAllowedException : SwoqActException { public MoveNotAllowedException() : base(ActResult.MoveNotAllowed) { } }
public class UnknownActionException : SwoqActException { public UnknownActionException() : base(ActResult.UnknownAction) { } }
public class GameFinishedException : SwoqActException { public GameFinishedException() : base(ActResult.GameFinished) { } }
public class UseNotAllowedException : SwoqActException { public UseNotAllowedException() : base(ActResult.UseNotAllowed) { } }
public class InventoryFullException : SwoqActException { public InventoryFullException() : base(ActResult.InventoryFull) { } }
public class InventoryEmptyException : SwoqActException { public InventoryEmptyException() : base(ActResult.InventoryEmpty) { } }
public class NoSwordException : SwoqActException { public NoSwordException() : base(ActResult.NoSword) { } }
public class Player1NotPresentException : SwoqActException { public Player1NotPresentException() : base(ActResult.PlayerNotPresent) { } }
public class Player2NotPresentException : SwoqActException { public Player2NotPresentException() : base(ActResult.Player2NotPresent) { } }
