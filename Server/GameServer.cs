using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

public class GameServer<MG>(ISwoqDatabase database, int nrActiveQuests = Parameters.NrOfActiveQuests) : IGameServer where MG : IMapGenerator
{
    private readonly Lock gamesWriteMutex = new();
    private ImmutableDictionary<Guid, IGame> games = ImmutableDictionary<Guid, IGame>.Empty;

    private readonly Lock currentQuestMutex = new();
    private ImmutableDictionary<Guid, string> currentQuestUserIds = ImmutableDictionary<Guid, string>.Empty;

    private readonly QuestQueue questQueue = new();

    private readonly Lock statisticsMutex = new();
    private DateTime lastStatisticsReportTime = DateTime.MinValue;
    private uint eventCount = 0;

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
            // Check that user is not already in a quest
            if (currentQuestUserIds.ContainsValue(user.Id))
            {
                throw new QuestAlreadyActiveException();
            }

            // Queue (or update entry) user
            questQueue.Enqueue(user);

            // Do not allow starting another quest when too many quests are active.
            if (currentQuestUserIds.Count >= nrActiveQuests)
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
            currentQuestUserIds = currentQuestUserIds.Add(quest.Id, user.Id);
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

        // Remove games that are finished from active quests list
        var idsFinished = games.Values.
            Where(g => g.State.IsFinished).
            Select(g => g.Id).
            ToImmutableArray();

        if (idsFinished.Length > 0)
        {
            lock (currentQuestMutex)
            {
                currentQuestUserIds = currentQuestUserIds.RemoveRange(idsFinished);
            }
        }

        // Remove games that have been finished for a while from the game list
        var idsToRemove = games.Values.
            Where(g => g.State.IsFinished).
            Where(g => now - g.LastActionTime > Parameters.GameRetentionTime).
            Select(g => g.Id).
            ToImmutableArray();

        if (idsToRemove.Length > 0)
        {
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
        var deltaTime = now - lastStatisticsReportTime;
        if (deltaTime < Parameters.StatisticsUpdatePeriod) return;

        lock (statisticsMutex)
        {
            // Extra check.
            // Maybe other thread already sent the update
            deltaTime = now - lastStatisticsReportTime;
            if (deltaTime < Parameters.StatisticsUpdatePeriod) return;

            var eventCount = Interlocked.Exchange(ref this.eventCount, 0);

            var eventsPerSecond = eventCount / (float)deltaTime.TotalSeconds;
            StatisticsUpdated?.Invoke(this, new(eventsPerSecond));
            lastStatisticsReportTime = now;
        }
    }
}
