using Swoq.Infra;
using Swoq.Server.Data;
using System.Collections.Immutable;

namespace Swoq.Server.Services;

public class GameServer(ISwoqDatabase database)
{
    public record StartResult(string PlayerName, Guid GameId, GameState State);

    public record QueuedQuest(string PlayerId, DateTime StartTime, DateTime LastQueryTime);

    private readonly object gamesWriteMutex = new();
    private IImmutableDictionary<Guid, IGame> games = ImmutableDictionary<Guid, IGame>.Empty;

    private readonly object currentQuestMutex = new();
    private Guid? currentQuestId = null;
    private IImmutableList<QueuedQuest> pendingQuests = [];

    public event EventHandler<IImmutableList<(string playerName, int queueTime)>>? QueueUpdated;

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

    private static Game StartTraining(Player player, int level)
    {
        // Check if player can play this level
        if (level < 0 || level > player.Level) throw new LevelNotAvailableException();
        // Create new training game
        return new Game(level, Parameters.MaxTrainingInactivityTime);
    }

    private Quest StartQuest(Player player)
    {
        if (player.Id == null) throw new ArgumentNullException(nameof(player));

        lock (currentQuestMutex)
        {
            var now = Clock.Now;

            // Cleanup inactive queued players.
            var inactiveQuests = pendingQuests.
                Where(q => q.LastQueryTime < now - Parameters.MaxQuestInactivityTime).
                ToImmutableArray();
            pendingQuests = pendingQuests.RemoveRange(inactiveQuests);

            // Cleanup current quest if finished or idle for too long
            if (currentQuestId.HasValue)
            {
                var currentQuest = games[currentQuestId.Value];
                if (currentQuest.State.Finished || currentQuest.IsInactive)
                {
                    currentQuestId = null;
                }
            }

            // Enqueue this player if not queued already
            var pendingQuest = pendingQuests.FirstOrDefault(q => q.PlayerId == player.Id);
            if (pendingQuest == null)
            {
                pendingQuest = new QueuedQuest(player.Id, now, now);
                pendingQuests = pendingQuests.Add(pendingQuest);
            }
            else
            {
                // Refresh query time of existing entry
                pendingQuests = pendingQuests.Replace(pendingQuest, pendingQuest with { LastQueryTime = now });
            }

            // Do not allow starting another quest when current quest is active.
            if (currentQuestId.HasValue)
            {
                NotifyQueueUpdate();
                throw new QuestQueuedException();
            }

            // Can only start when first in line
            var firstPendingQuest = pendingQuests.OrderBy(q => q.StartTime).First();
            if (firstPendingQuest.PlayerId != player.Id)
            {
                NotifyQueueUpdate();
                throw new QuestQueuedException();
            }

            // No other quest active and first in queue
            pendingQuests = pendingQuests.Remove(firstPendingQuest);
            NotifyQueueUpdate();

            // Start a new game
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

    private void NotifyQueueUpdate()
    {
        // TODO: decouple on background thread?
        // Only update every X seconds
        var now = Clock.Now;

        (string playerName, int queueTime) Convert(QueuedQuest qq)
        {
            var playerName = database.FindPlayerByIdAsync(qq.PlayerId)?.Result?.Name ?? "Unknown";
            var queueTime = (int)Math.Round((now - qq.StartTime).TotalSeconds);
            return (playerName, queueTime);
        }

        var queue = pendingQuests.Select(qq => Convert(qq)).ToImmutableArray();

        QueueUpdated?.Invoke(this, queue);
    }
}
