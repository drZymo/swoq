using Bot;
using Swoq.Interface;

int? level = 0; // null for quest, integer for train
int? seed = null; // null for random seed, integer for fixed seed

using var connection = new GameConnection();

using var game = connection.Start(level, seed);

Console.WriteLine($"Game {game.GameId} started");
if (game.Seed.HasValue) Console.WriteLine($"- seed: {game.Seed.Value}");
Console.WriteLine($"- map size: {game.MapHeight}x{game.MapWidth}");

var moveEast = true;
while (game.State.Status == GameStatus.Active)
{
    var action = moveEast ? DirectedAction.MoveEast : DirectedAction.MoveSouth;
    Console.WriteLine($"tick: {game.State.Tick}, action: {action}");
    game.Act(action);
    moveEast = !moveEast;
}
