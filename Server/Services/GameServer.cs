using Swoq.Server.Data;
using System.Collections.Immutable;

namespace Swoq.Server.Services;

internal class GameServer(ISwoqDatabase database)
{
    public record StartResult(string PlayerName, Guid GameId, int Height, int Width, int VisibilityRange, GameState State);

    private readonly object gamesWriteMutex = new();
    private IImmutableDictionary<Guid, IGame> games = ImmutableDictionary<Guid, IGame>.Empty;

    public StartResult Start(string playerId, int? level)
    {
        // Check if player can play this level
        if (level < 0) throw new LevelNotAvailableException();
        var player = database.FindPlayerByIdAsync(playerId).Result ?? throw new UnknownPlayerException();
        if (level > player.Level) throw new LevelNotAvailableException();

        // Create a new game
        IGame game = level.HasValue ? new Game(level.Value) : new Quest(player, database);
        lock (gamesWriteMutex)
        {
            games = games.Add(game.Id, game);
        }

        CleanupOldGames();

        // Return initial state of game
        var state = game.State;
        return new StartResult(player.Name, game.Id, Parameters.MapHeight, Parameters.MapWidth, Parameters.PlayerVisibilityRange, state);
    }

    public GameState Act(Guid gameId, DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        // Does game exist?
        if (!games.TryGetValue(gameId, out var game)) throw new UnknownGameIdException();

        // Play game
        game.Act(action1, action2);
        return game.State;
    }

    private void CleanupOldGames()
    {
        // Gather ids to remove
        var idsToRemove = ImmutableList<Guid>.Empty;
        var now = DateTime.Now;
        foreach (var game in games.Values)
        {
            var age = now - game.LastAction;
            if (age > Parameters.MaxGameIdleTime)
            {
                idsToRemove = idsToRemove.Add(game.Id);
            }
        }

        // Remove all at once
        if (idsToRemove.Count > 0)
        {
            lock (gamesWriteMutex)
            {
                games = games.RemoveRange(idsToRemove);
            }
        }
    }
}
