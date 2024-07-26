using System.Collections.Immutable;

namespace Swoq.Server;

public class TrainingServer
{
    private readonly object gamesWriteMutex = new();
    private IImmutableDictionary<Guid, Game> games = ImmutableDictionary<Guid, Game>.Empty;

    public (Guid gameId, int height, int width, GameState state) StartGame()
    {
        var game = new Game();

        lock (gamesWriteMutex)
        {
            games = games.Add(game.Id, game);
        }

        var state = game.GetState();

        return (game.Id, game.Height, game.Width, state);
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
