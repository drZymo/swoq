namespace Swoq.Server;

public abstract class SwoqException : Exception;

public class PlayerAlreadyRegisteredException : SwoqException;
public class UnknownPlayerException : SwoqException;
public class UnknownGameIdException : SwoqException;
public class LevelNotAvailableException : SwoqException;
public class QuestQueuedException : SwoqException;

public abstract class SwoqGameException(GameState state) : SwoqException
{
    public GameState State { get; } = state;
}

public class MoveNotAllowedException(GameState state) : SwoqGameException(state);
public class UseNotAllowedException(GameState state) : SwoqGameException(state);
public class UnknownActionException(GameState state) : SwoqGameException(state);
public class UnknownDirectionException(GameState state) : SwoqGameException(state);
public class GameFinishedException(GameState state) : SwoqGameException(state);
public class Player1NotPresentException(GameState state) : SwoqGameException(state);
public class Player2NotPresentException(GameState state) : SwoqGameException(state);
public class InventoryEmptyException(GameState state) : SwoqGameException(state);
public class InventoryFullException(GameState state) : SwoqGameException(state);
public class NoSwordException(GameState state) : SwoqGameException(state);
public class Player1DiedException(GameState state) : SwoqGameException(state);
public class Player2DiedException(GameState state) : SwoqGameException(state);
public class NoProgressException(GameState state) : SwoqGameException(state);
public class GameTimeoutException(GameState state) : SwoqGameException(state);

