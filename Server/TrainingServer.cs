using Swoq.Server.Services;
using System.Collections.Immutable;

namespace Swoq.Server;

internal class TrainingServer(ISwoqDatabase database)
{
    public record StartResult(Guid GameId, int Height, int Width, int VisibilityRange, GameState State);

    private readonly object gamesWriteMutex = new();
    private IImmutableDictionary<Guid, Game> games = ImmutableDictionary<Guid, Game>.Empty;

    public StartResult Start(string playerId, int level)
    {
        var player = database.FindPlayerByIdAsync(playerId).Result ?? throw new UnknownPlayerException();

        if (level < 0 && level > player.Level) throw new LevelNotAvailableException();

        var game = new Game(level);

        lock (gamesWriteMutex)
        {
            games = games.Add(game.Id, game);
        }

        var state = game.GetState();

        return new StartResult(game.Id, game.Height, game.Width, Parameters.PlayerVisibilityRange, state);
    }

    public GameState Act(Guid gameId, DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        if (!games.TryGetValue(gameId, out var game))
        {
            throw new UnknownGameIdException();
        }

        game.Act(action1, action2);
        return game.GetState();
    }
}
