using Bot;
using Swoq.Interface;

using var connection = new GameConnection();

using var game = connection.Start(null); // null for quest, integer for train
Console.WriteLine($"game id: {game.GameId}");
Console.WriteLine($"map size: {game.MapHeight}x{game.MapWidth}");

var moveEast = true;
while (game.State.Status == GameStatus.Active)
{
    var action = moveEast ? DirectedAction.MoveEast : DirectedAction.MoveSouth;
    Console.WriteLine($"tick: {game.State.Tick}, action: {action}");
    game.Act(action);
    moveEast = !moveEast;
}
