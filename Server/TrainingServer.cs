using System.Collections.Immutable;

namespace Swoq.Server;

public class TrainingServer
{
    private readonly object gamesWriteMutex = new();
    private IImmutableDictionary<Guid, Game> games = ImmutableDictionary<Guid, Game>.Empty;

    public (Guid gameId, int[] state) StartGame()
    {
        var game = new Game();

        lock (gamesWriteMutex)
        {
            games = games.Add(game.Id, game);
        }

        var state = game.GetState();

        return (game.Id, state);
    }

    public (bool success, int[] state) Move(Guid gameId, Direction direction)
    {
        if (!games.TryGetValue(gameId, out var game))
        {
            return (false, []);
        }

        var success = game.Move(direction);
        var state = game.GetState();
        return (success, state);
    }
}
