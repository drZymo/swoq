using Swoq.Server.Services;
using System.Collections.Immutable;

namespace Swoq.Server;

internal class TrainingServer(ISwoqDatabase database)
{
    private readonly object gamesWriteMutex = new();
    private IImmutableDictionary<Guid, Game> games = ImmutableDictionary<Guid, Game>.Empty;

    public (Guid gameId, int height, int width, GameState state) StartGame(string playerId, int level)
    {
        var player = database.FindPlayerByIdAsync(playerId).Result ?? throw new PlayerUnknownException();

        if (level < 0 && level > player.Level) throw new LevelNotAvailableException();

        var game = new Game(level);

        lock (gamesWriteMutex)
        {
            games = games.Add(game.Id, game);
        }

        var state = game.GetState();

        return (game.Id, Game.Height, Game.Width, state);
    }

    public (bool success, GameState state) Move(Guid gameId, Direction direction)
    {
        if (!games.TryGetValue(gameId, out var game))
        {
            return (false, new GameState([], false, 0));
        }

        var success = game.Move(direction);
        var state = game.GetState();
        return (success, state);
    }

    public (bool success, GameState state) Use(Guid gameId, Direction direction)
    {
        if (!games.TryGetValue(gameId, out var game))
        {
            return (false, new GameState([], false, 0));
        }

        var success = game.Use(direction);
        var state = game.GetState();
        return (success, state);
    }
}
