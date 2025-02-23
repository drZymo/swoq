namespace Swoq.Interface;

public static class Converters
{
    public static string ConvertToString(this StartResult result) => result switch
    {
        StartResult.Ok => "OK",
        StartResult.InternalError => "InternalError",
        StartResult.UnknownUser => "NoSword",
        StartResult.UserLevelTooLow => "UserLevelTooLow",
        StartResult.QuestQueued => "QuestQueued",
        StartResult.QuestAlreadyActive => "QuestAlreadyActive",
        _ => "Unknown",
    };

    public static string ConvertToString(this ActResult result) => result switch
    {
        ActResult.Ok => "OK",
        ActResult.InternalError => "InternalError",
        ActResult.UnknownGameId => "UnknownGameId",
        ActResult.MoveNotAllowed => "MoveNotAllowed",
        ActResult.UseNotAllowed => "UseNotAllowed",
        ActResult.UnknownAction => "UnknownAction",
        ActResult.GameFinished => "GameFinished",
        ActResult.PlayerNotPresent => "PlayerNotPresent",
        ActResult.Player2NotPresent => "Player2NotPresent",
        ActResult.InventoryFull => "InventoryFull",
        ActResult.InventoryEmpty => "InventoryEmpty",
        ActResult.NoSword => "NoSword",
        _ => "Unknown",
    };

    public static string ConvertToString(this DirectedAction action) => action switch
    {
        DirectedAction.None => "None",
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
