using System.Collections.Immutable;

namespace Swoq.Server;

public class TrainingServer
{
    private readonly object gamesLock = new();
    private IImmutableDictionary<Guid, Game> games = ImmutableDictionary<Guid, Game>.Empty;

    public Guid StartGame()
    {
        var game = new Game();

        lock (gamesLock)
        {
            games = games.Add(game.Id, game);
        }

        return game.Id;
    }

    public int[] GetGameState(Guid gameId)
    {
        Game? game = null;
        lock (gamesLock)
        {
            if (!games.TryGetValue(gameId, out game))
            {
                game = null;
            }
        }
        if (game == null) return [];

        return game.GetState();
    }
}
