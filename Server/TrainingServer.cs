using System.Collections.Immutable;

namespace Swoq.Server;

public class TrainingServer
{
    private readonly object gamesWriteMutex = new();
    private IImmutableDictionary<Guid, Game> games = ImmutableDictionary<Guid, Game>.Empty;

    public (Guid gameId, int height, int width, int[] map) StartGame()
    {
        var game = new Game();

        lock (gamesWriteMutex)
        {
            games = games.Add(game.Id, game);
        }

        var state = game.GetState();

        return (game.Id, game.Height, game.Width, state.map);
    }

    public (bool success, int[] map, bool finished) Move(Guid gameId, Direction direction)
    {
        if (!games.TryGetValue(gameId, out var game))
        {
            return (false, [], true);
        }

        var success = game.Move(direction);
        var state = game.GetState();
        return (success, state.map, state.finished);
    }
}
