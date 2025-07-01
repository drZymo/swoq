namespace Swoq.Interface;

public static class Converters
{
    public static string ConvertToString(this StartResult result) => result switch
    {
        StartResult.Ok => "OK",
        StartResult.InternalError => "Internal error",
        StartResult.UnknownUser => "No sword",
        StartResult.InvalidLevel => "Invalid level",
        StartResult.QuestQueued => "Quest queued",
        StartResult.NotAllowed => "Not allowed",
        _ => "Unknown",
    };

    public static string ConvertToString(this ActResult result) => result switch
    {
        ActResult.Ok => "OK",
        ActResult.InternalError => "Internal error",
        ActResult.UnknownGameId => "Unknown game ID",
        ActResult.MoveNotAllowed => "Move not allowed",
        ActResult.UseNotAllowed => "Use not allowed",
        ActResult.UnknownAction => "Unknown action",
        ActResult.GameFinished => "Game finished",
        ActResult.PlayerNotPresent => "Player not present",
        ActResult.Player2NotPresent => "Player 2 not present",
        ActResult.InventoryFull => "Inventory full",
        ActResult.InventoryEmpty => "Inventory empty",
        ActResult.NoSword => "No sword",
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
