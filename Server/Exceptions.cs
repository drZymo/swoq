namespace Swoq.Server;

internal abstract class SwoqException : Exception;

internal class PlayerAlreadyRegisteredException : SwoqException;
internal class UnknownPlayerException : SwoqException;
internal class UnknownGameIdException : SwoqException;
internal class LevelNotAvailableException : SwoqException;
internal class QuestQueuedException : SwoqException;

internal abstract class SwoqGameException(GameState state) : SwoqException
{
    public GameState State { get; } = state;
}

internal class MoveNotAllowedException(GameState state) : SwoqGameException(state);
internal class UseNotAllowedException(GameState state) : SwoqGameException(state);
internal class UnknownActionException(GameState state) : SwoqGameException(state);
internal class UnknownDirectionException(GameState state) : SwoqGameException(state);
internal class GameFinishedException(GameState state) : SwoqGameException(state);
internal class Player1NotPresentException(GameState state) : SwoqGameException(state);
internal class Player2NotPresentException(GameState state) : SwoqGameException(state);
internal class InventoryEmptyException(GameState state) : SwoqGameException(state);
internal class InventoryFullException(GameState state) : SwoqGameException(state);
internal class NoSwordException(GameState state) : SwoqGameException(state);
internal class Player1DiedException(GameState state) : SwoqGameException(state);
internal class Player2DiedException(GameState state) : SwoqGameException(state);
internal class QuestTimedOutException(GameState state) : SwoqGameException(state);

