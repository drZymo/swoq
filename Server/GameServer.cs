using Swoq.Infra;
using Swoq.Server.Data;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

public class GameServer(ISwoqDatabase database, IMapGenerator mapGenerator)
{
    public record StartResult(string PlayerName, Guid GameId, GameState State);

    private readonly object gamesWriteMutex = new();
    private IImmutableDictionary<Guid, IGame> games = ImmutableDictionary<Guid, IGame>.Empty;

    private readonly object currentQuestMutex = new();
    private Guid? currentQuestId = null;

    private readonly QuestQueue questQueue = new();

    public event EventHandler<IImmutableList<string>>? QueueUpdated
    {
        add => questQueue.Updated += value;
        remove => questQueue.Updated -= value;
    }

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

        var map = mapGenerator.Generate(level);

		// Create new training game
		return new Game(map, Parameters.MaxTrainingInactivityTime);
    }

    private Quest StartQuest(Player player)
    {
        if (player.Id == null) throw new ArgumentNullException(nameof(player));

        lock (currentQuestMutex)
        {
            var now = Clock.Now;

            // Cleanup current quest if finished or idle for too long
            if (currentQuestId.HasValue)
            {
                var currentQuest = games[currentQuestId.Value];
                if (currentQuest.State.Finished || currentQuest.IsInactive)
                {
                    currentQuestId = null;
                }
            }

            // Queue (or update entry) player
            questQueue.Enqueue(player);

            // Do not allow starting another quest when current quest is active.
            if (currentQuestId.HasValue)
            {
                throw new QuestQueuedException();
            }

            // Is this player the first entry in the queue?
            if (questQueue.FrontPlayerId != player.Id)
            {
                throw new QuestQueuedException();
            }
            var (frontPlayerId, frontPlayerName) = questQueue.Dequeue();
            Debug.Assert(frontPlayerId == player.Id);
            Debug.Assert(frontPlayerName == player.Name);

            // No quest active
            // Player first in queue
            // Start a new game
            var quest = new Quest(player, database, mapGenerator);
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
        var now = Clock.Now;

        // Gather ids to remove
        var idsToRemove = games.Values.
            Where(g => now - g.LastActionTime > Parameters.GameRetentionTime).
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
