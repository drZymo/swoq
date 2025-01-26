namespace Swoq.Server;

public abstract class SwoqException : Exception;

public class UnknownUserException : SwoqException;
public class UnknownGameIdException : SwoqException;
public class UserLevelTooLowException : SwoqException;
public class QuestQueuedException : SwoqException;
public class QuestAlreadyActiveException : SwoqException;
public class MoveNotAllowedException : SwoqException;
public class UnknownActionException : SwoqException;
public class GameFinishedException : SwoqException;
public class UseNotAllowedException : SwoqException;
public class InventoryFullException : SwoqException;
public class InventoryEmptyException : SwoqException;
public class NoSwordException : SwoqException;
public class Player1NotPresentException : SwoqException;
public class Player2NotPresentException : SwoqException;
