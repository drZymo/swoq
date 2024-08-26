namespace Swoq.Interface;

public static class Converters
{
    public static string ConvertToString(this Result result) => result switch
    {
        Result.Ok => "OK",
        Result.InternalError => "InternalError",
        Result.UnknownUser => "UnknownUser",
        Result.UnknownGameId => "UnknownGameId",
        Result.UserLevelTooLow => "UserLevelTooLow",
        Result.QuestQueued => "QuestQueued",
        Result.MoveNotAllowed => "MoveNotAllowed",
        Result.UseNotAllowed => "UseNotAllowed",
        Result.UnknownAction => "UnknownAction",
        Result.GameFinished => "GameFinished",
        Result.PlayerNotPresent => "PlayerNotPresent",
        Result.Player2NotPresent => "Player2NotPresent",
        Result.InventoryFull => "InventoryFull",
        Result.InventoryEmpty => "InventoryEmpty",
        Result.NoSword => "NoSword",
        Result.PlayerDied => "PlayerDied",
        Result.Player2Died => "Player2Died",
        Result.NoProgress => "NoProgress",
        _ => "Unknown",
    };

    public static string ConvertToString(this DirectedAction action) => action switch
    {
        DirectedAction.MoveNorth => "Move North",
        DirectedAction.MoveEast => "Move East",
        DirectedAction.MoveSouth => "Move South",
        DirectedAction.MoveWest => "Move West",
        DirectedAction.UseNorth => "Use North",
        DirectedAction.UseEast => "Use East",
        DirectedAction.UseSouth => "Use South",
        DirectedAction.UseWest => "Use West",
        _ => "Unknown",
    };
}
