using Swoq.Interface;

namespace Swoq.Server;

public abstract class SwoqException : Exception;

// Start
public abstract class SwoqStartException(StartResult result) : Exception
{
    public StartResult Result { get; } = result;
}
public class UnknownUserException() : SwoqStartException(StartResult.UnknownUser) { }
public class InvalidLevelException() : SwoqStartException(StartResult.InvalidLevel) { }
public class QuestQueuedException() : SwoqStartException(StartResult.QuestQueued) { }

// Act
public abstract class SwoqActException(ActResult result) : Exception
{
    public ActResult Result { get; } = result;
}
public class UnknownGameIdException() : SwoqActException(ActResult.UnknownGameId) { }
public class MoveNotAllowedException() : SwoqActException(ActResult.MoveNotAllowed) { }
public class UnknownActionException() : SwoqActException(ActResult.UnknownAction) { }
public class GameFinishedException() : SwoqActException(ActResult.GameFinished) { }
public class UseNotAllowedException() : SwoqActException(ActResult.UseNotAllowed) { }
public class InventoryFullException() : SwoqActException(ActResult.InventoryFull) { }
public class InventoryEmptyException() : SwoqActException(ActResult.InventoryEmpty) { }
public class NoSwordException() : SwoqActException(ActResult.NoSword) { }
public class Player1NotPresentException() : SwoqActException(ActResult.PlayerNotPresent) { }
public class Player2NotPresentException() : SwoqActException(ActResult.Player2NotPresent) { }
