using Swoq.Server.Data;
using System.Collections.Immutable;

namespace Swoq.Server.Services;

internal class GameServer(ISwoqDatabase database)
{
    public record StartResult(string PlayerName, Guid GameId, GameState State);

    private readonly object gamesWriteMutex = new();
    private IImmutableDictionary<Guid, IGame> games = ImmutableDictionary<Guid, IGame>.Empty;

    private readonly object currentQuestMutex = new();
    private Guid? currentQuestId = null;

    public StartResult Start(string playerId, int? level)
    {
        // Get player object
        var player = database.FindPlayerByIdAsync(playerId).Result ?? throw new UnknownPlayerException();

        // Create a new game
        IGame game = level.HasValue ? StartTraining(player, level.Value) : StartQuest(player);
        lock (gamesWriteMutex)
        {
            games = games.Add(game.Id, game);
        }
        // and remove old games        
        CleanupOldGames();

        return new StartResult(player.Name, game.Id, game.State);
    }

    private Game StartTraining(Player player, int level)
    {
        // Check if player can play this level
        if (level < 0 || level > player.Level) throw new LevelNotAvailableException();
        // Create new training game
        return new Game(level, Parameters.MaxTrainingInactivityTime);
    }

    private Quest StartQuest(Player player)
    {
        // TODO: Enqueue this quest

        lock (currentQuestMutex)
        {
            if (currentQuestId.HasValue)
            {
                // Cleanup current quest if finished or idle for too long
                var currentQuest = games[currentQuestId.Value];
                if (currentQuest.State.Finished || currentQuest.IsInactive)
                {
                    currentQuestId = null;
                }
            }

            // Do not allow starting another quest when current quest is active.
            if (currentQuestId.HasValue)
            {
                throw new QuestQueuedException();
            }

            // No other quest active, so make a new quest
            var quest = new Quest(player, database);
            currentQuestId = quest.Id;
            return quest;
        }
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
        var idsToRemove = games.Values.
            Where(g => g.IsInactive).
            Select(g => g.Id).
            ToImmutableArray();

        // Remove all at once
        if (idsToRemove.Length > 0)
        {
            lock (gamesWriteMutex)
            {
                games = games.RemoveRange(idsToRemove);
            }
        }
    }
}
