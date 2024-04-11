using System.Collections.Immutable;

namespace Swoq.Server;

public class TrainingServer
{
    private readonly object gamesLock = new();
    private IImmutableDictionary<Guid, Game> games = ImmutableDictionary<Guid, Game>.Empty;

    public (Guid gameId, int[] state) StartGame()
    {
        var game = new Game();

        lock (gamesLock)
        {
            games = games.Add(game.Id, game);
        }

        var state = game.GetState();

        return (game.Id, state);
    }

    public int[] Move(Guid gameId, Direction direction)
    {
        var game = TryGetGame(gameId);
        if (game == null) return [];

        game.Move(direction);
        return game.GetState();
    }


    private Game? TryGetGame(Guid gameId)
    {
        Game? game = null;
        lock (gamesLock)
        {
            if (!games.TryGetValue(gameId, out game))
            {
                game = null;
            }
        }

        return game;
    }
}
