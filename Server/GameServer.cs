using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

public class GameServer<MG>(ISwoqDatabase database, int nrActiveQuests = Parameters.NrOfActiveQuests) : IGameServer where MG : IMapGenerator
{
    private readonly Lock gamesWriteMutex = new();
    private IImmutableDictionary<Guid, IGame> games = ImmutableDictionary<Guid, IGame>.Empty;

    private readonly Lock currentQuestMutex = new();
    private ImmutableHashSet<Guid> currentQuestIds = [];

    private readonly QuestQueue questQueue = new();

    private readonly Lock statisticsMutex = new();
    private int eventCount = 0;
    private DateTime lastStatisticsReported = DateTime.MinValue;

    public event EventHandler<GameAddedEventArgs>? GameAdded;
    public event EventHandler<GameRemovedEventArgs>? GameRemoved;
    public event EventHandler<GameUpdatedEventArgs>? GameUpdated;

    public event EventHandler<QueueUpdatedEventArgs>? QueueUpdated
    {
        add => questQueue.Updated += value;
        remove => questQueue.Updated -= value;
    }

    public event EventHandler<StatisticsUpdatedEventArgs>? StatisticsUpdated;

    public GameStartResult Start(string userId, int? level)
    {
        var user = GetUserOrThrow(database, userId);

        // First remove old games
        CleanupOldGames();

        // Create a new game
        IGame game = level.HasValue ? StartTraining(user, level.Value) : StartQuest(user);
        lock (gamesWriteMutex)
        {
            games = games.Add(game.Id, game);
            GameAdded?.Invoke(this, new GameAddedEventArgs(game.Id, user.Name, game.Level, !level.HasValue));
        }

        RegisterActivity();

        return new GameStartResult(user.Name, game.Id, game.State);
    }

    private static User GetUserOrThrow(ISwoqDatabase database, string userId)
    {
        User user;
        try
        {
            // Get user object
            user = database.FindUserByIdAsync(userId).Result ?? throw new UnknownUserException();
        }
        catch
        {
            throw new UnknownUserException();
        }
        return user;
    }

    private static Game StartTraining(User user, int level)
    {
        // Check if user can play this level
        if (level < 0 || user.Level < level) throw new UserLevelTooLowException();

        var map = MG.Generate(level, Parameters.MapHeight, Parameters.MapWidth);

        // Create new training game
        return new Game(map, Parameters.MaxTrainingInactivityTime);
    }

    private Quest<MG> StartQuest(User user)
    {
        if (user.Id == null) throw new ArgumentNullException(nameof(user));

        lock (currentQuestMutex)
        {
            // Cleanup current quest if finished or idle for too long
            foreach (var currentQuestId in currentQuestIds)
            {
                var currentQuest = games[currentQuestId];
                currentQuest.CheckGameIsFinished();
                if (currentQuest.State.IsFinished)
                {
                    currentQuestIds = currentQuestIds.Remove(currentQuestId);
                }
            }

            // Queue (or update entry) user
            questQueue.Enqueue(user);

            // Do not allow starting another quest when too many quests are active.
            if (currentQuestIds.Count >= nrActiveQuests)
            {
                throw new QuestQueuedException();
            }

            // Is this user the first entry in the queue?
            if (questQueue.FrontUserId != user.Id)
            {
                throw new QuestQueuedException();
            }

            // No quests active
            // User first in queue
            // Dequeue
            var (frontUserId, frontUserName) = questQueue.Dequeue();
            Debug.Assert(frontUserId == user.Id);
            Debug.Assert(frontUserName == user.Name);
            // and start a new game
            var quest = new Quest<MG>(user, database);
            currentQuestIds = currentQuestIds.Add(quest.Id);
            return quest;
        }
    }

    public GameState Act(Guid gameId, DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        // Does game exist?
        if (!games.TryGetValue(gameId, out var game)) throw new UnknownGameIdException();

        // Play game
        var prevLevel = game.Level;
        var prevStatus = game.State.Status;
        game.Act(action1, action2);
        if (game.Level != prevLevel || game.State.Status != prevStatus)
        {
            GameUpdated?.Invoke(this, new GameUpdatedEventArgs(gameId, game.Level, game.State.IsFinished));
        }

        RegisterActivity();

        return game.State;
    }

    private void CleanupOldGames()
    {
        var now = Clock.Now;

        // Check status of all active games
        foreach (var game in games.Values.Where(g => !g.State.IsFinished))
        {
            game.CheckGameIsFinished();
        }

        // Find games that have been finished for a while
        var idsToRemove = games.Values.
            Where(g => g.State.IsFinished).
            Where(g => now - g.LastActionTime > Parameters.GameRetentionTime).
            Select(g => g.Id).
            ToImmutableArray();

        if (idsToRemove.Length > 0)
        {
            // Update current quest before actually removing the games
            lock (currentQuestMutex)
            {
                foreach (var gameId in idsToRemove)
                {
                    currentQuestIds = currentQuestIds.Remove(gameId);
                }
            }
            // Remove all games in a batch
            lock (gamesWriteMutex)
            {
                games = games.RemoveRange(idsToRemove);
            }
            // Notify monitors
            foreach (var id in idsToRemove)
            {
                GameRemoved?.Invoke(this, new GameRemovedEventArgs(id));
            }
        }
    }

    private void RegisterActivity()
    {
        // Thread-safe increment
        Interlocked.Increment(ref eventCount);

        // Should we send an update
        var now = DateTime.Now;
        var delta = now - lastStatisticsReported;
        if (delta < Parameters.StatisticsUpdatePeriod) return;

        lock (statisticsMutex)
        {
            // Extra check.
            // Maybe other thread already sent the update
            delta = now - lastStatisticsReported;
            if (delta < Parameters.StatisticsUpdatePeriod) return;

            var eventsPerSecond = eventCount / (float)delta.TotalSeconds;
            StatisticsUpdated?.Invoke(this, new(eventsPerSecond));

            eventCount = 0;
            lastStatisticsReported = now;
        }
    }
}
