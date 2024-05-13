namespace Swoq.Interface;

public static class Converters
{
    public static string ConvertToString(this Result result) => result switch
    {
        Result.Ok => "OK",
        Result.InternalError => "InternalError",
        Result.PlayerAlreadyRegistered => "PlayerAlreadyRegistered",
        Result.UnknownPlayer => "UnknownPlayer",
        Result.UnknownGameId => "UnknownGameId",
        Result.LevelNotAvailable => "LevelNotAvailable",
        Result.QuestQueued => "QuestQueued",
        Result.MoveNotAllowed => "MoveNotAllowed",
        Result.UseNotAllowed => "UseNotAllowed",
        Result.UnknownAction => "UnknownAction",
        Result.UnknownDirection => "UnknownDirection",
        Result.GameFinished => "GameFinished",
        Result.Player1NotPresent => "Player1NotPresent",
        Result.Player2NotPresent => "Player2NotPresent",
        Result.InventoryFull => "InventoryFull",
        Result.InventoryEmpty => "InventoryEmpty",
        Result.NoSword => "NoSword",
        Result.Player1Died => "Player1Died",
        Result.Player2Died => "Player2Died",
        Result.NoProgress => "NoProgress",
        Result.GameTimeout => "GameTimeout",
        _ => "Unknown",
    };

    public static string ConvertToString(this Action action) => action switch
    {
        Action.Use => "Use",
        Action.Move => "Move",
        _ => "Unknown",
    };

    public static string ConvertToString(this Direction direction) => direction switch
    {
        Direction.North => "North",
        Direction.East => "East",
        Direction.South => "South",
        Direction.West => "West",
        _ => "Unknown",
    };
}
