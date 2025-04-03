using Bot;
using Swoq.Interface;

DotEnv.Load();

var userId = Environment.GetEnvironmentVariable("SWOQ_USER_ID");
var userName = Environment.GetEnvironmentVariable("SWOQ_USER_NAME");
var host = Environment.GetEnvironmentVariable("SWOQ_HOST");
ArgumentException.ThrowIfNullOrWhiteSpace(userId);
ArgumentException.ThrowIfNullOrWhiteSpace(userName);
ArgumentException.ThrowIfNullOrWhiteSpace(host);
using var connection = new GameConnection(userId, userName, host);

var level = DotEnv.GetEnvironmentVariableInt("SWOQ_LEVEL");
var seed = DotEnv.GetEnvironmentVariableInt("SWOQ_SEED");
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
