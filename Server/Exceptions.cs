namespace Swoq.Server;

public abstract class SwoqException : Exception;

public class UnknownUserException : SwoqException;
public class UnknownGameIdException : SwoqException;
public class UserLevelTooLowException : SwoqException;
public class QuestQueuedException : SwoqException;

public abstract class SwoqGameException(GameState state) : SwoqException
{
    public GameState State { get; } = state;
}

// Action not allowed
public abstract class SwoqActionNotAllowedException(GameState state) :
    SwoqGameException(state)
{ }
public class MoveNotAllowedException(GameState state) : SwoqActionNotAllowedException(state);
public class UseNotAllowedException(GameState state) : SwoqActionNotAllowedException(state);
public class UnknownActionException(GameState state) : SwoqActionNotAllowedException(state);
public class GameFinishedException(GameState state) : SwoqActionNotAllowedException(state);
public class Player1NotPresentException(GameState state) : SwoqActionNotAllowedException(state);
public class Player2NotPresentException(GameState state) : SwoqActionNotAllowedException(state);
public class InventoryEmptyException(GameState state) : SwoqActionNotAllowedException(state);
public class InventoryFullException(GameState state) : SwoqActionNotAllowedException(state);
public class NoSwordException(GameState state) : SwoqActionNotAllowedException(state);


// Game ended reasons
public abstract class SwoqGameEndedException(GameState state) :
    SwoqGameException(state)
{ }
public class Player1DiedException(GameState state) : SwoqGameEndedException(state);
public class Player2DiedException(GameState state) : SwoqGameEndedException(state);
public class NoProgressException(GameState state) : SwoqGameEndedException(state);

