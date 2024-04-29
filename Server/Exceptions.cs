namespace Swoq.Server;


internal class PlayerAlreadyRegisteredException : Exception;
internal class UnknownPlayerException : Exception;
internal class UnknownGameIdException : Exception;
internal class LevelNotAvailableException : Exception;

internal class SwoqException(GameState state) : Exception
{
    public GameState State { get; private set; } = state;
}

internal class MoveNotAllowedException(GameState state) : SwoqException(state);
internal class UseNotAllowedException(GameState state) : SwoqException(state);
internal class UnknownActionException(GameState state) : SwoqException(state);
internal class UnknownDirectionException(GameState state) : SwoqException(state);
internal class GameFinishedException(GameState state) : SwoqException(state);
internal class Player1NotPresentException(GameState state) : SwoqException(state);
internal class Player2NotPresentException(GameState state) : SwoqException(state);
internal class InventoryEmptyException(GameState state) : SwoqException(state);
internal class InventoryFullException(GameState state) : SwoqException(state);
internal class NoSwordException(GameState state) : SwoqException(state);
internal class Player1DiedException(GameState state) : SwoqException(state);
internal class Player2DiedException(GameState state) : SwoqException(state);
internal class UnknownQuestIdException(GameState state) : SwoqException(state);

