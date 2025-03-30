using Bot;
using Swoq.Interface;

using var connection = new GameConnection(DotEnv.UserId, DotEnv.UserName, DotEnv.Host);

using var game = connection.Start(DotEnv.Level, DotEnv.Seed);

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
