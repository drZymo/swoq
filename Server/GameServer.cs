using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Swoq.Server;

public class GameServerStartException(StartResult result, Exception? innerException = null) : Exception($"Start result {result}", innerException)
{
    public StartResult Result { get; } = result;
}

public class GameServerActException(ActResult result, GameState? state, Exception? innerException = null) : Exception($"Act result {result}", innerException)
{
    public ActResult Result { get; } = result;
    public GameState? State { get; } = state;
}

internal abstract class GameServerBase(IMapGenerator mapGenerator, ISwoqDatabase database) : IGameServer
{
    protected readonly IMapGenerator mapGenerator = mapGenerator;
    protected readonly ISwoqDatabase database = database;

    protected readonly ConcurrentDictionary<Guid, IGame> games = new();
    protected readonly QuestQueue questQueue = new();

    public event EventHandler<GameRemovedEventArgs>? GameRemoved;

    public event EventHandler<QueueUpdatedEventArgs>? QueueUpdated
    {
        add => questQueue.Updated += value;
        remove => questQueue.Updated -= value;
    }

    public GameStartResult Start(string userId, int? level, int? seed = null)
    {
        try
        {
            var user = GetUserOrThrow(database, userId);

            // If seed is not given, use a random one.
            var actualSeed = seed ?? Random.Shared.Next();

            // Create a new game
            IGame game;
            if (level.HasValue)
            {
                // Check if user can play this level
                if (level < 0 || level > user.Level || level > mapGenerator.MaxLevel) throw new InvalidLevelException();
                game = StartTraining(user, level.Value, ref actualSeed);
            }
            else
            {
                game = StartQuest(user, ref actualSeed);
            }

            if (!games.TryAdd(game.Id, game))
            {
                throw new InvalidOperationException("Game could not be added");
            }

            return new GameStartResult(user.Name, game.Id, game.State, actualSeed);
        }
        catch (SwoqStartException ex)
        {
            throw new GameServerStartException(ex.Result, ex);
        }
        catch (Exception ex)
        {
            throw new GameServerStartException(StartResult.InternalError, ex);
        }
    }

    private static User GetUserOrThrow(ISwoqDatabase database, string userId)
    {
        try
        {
            return database.FindUserByIdAsync(userId).Result ?? throw new UnknownUserException();
        }
        catch
        {
            throw new UnknownUserException();
        }
    }

    protected abstract Game StartTraining(User user, int level, ref int seed);

    protected abstract Quest StartQuest(User user, ref int seed);

    public GameState Act(Guid gameId, DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        // Try to find game
        if (!games.TryGetValue(gameId, out var game)) throw new GameServerActException(ActResult.UnknownGameId, null);

        // Play game
        try
        {
            game.Act(action1, action2);
            return game.State;
        }
        catch (SwoqActException ex)
        {
            throw new GameServerActException(ex.Result, game.State, ex);
        }
        catch (Exception ex)
        {
            throw new GameServerActException(ActResult.InternalError, game.State, ex);
        }
    }

    protected void RemoveGame(Guid gameId)
    {
        if (games.TryRemove(gameId, out var game))
        {
            GameRemoved?.Invoke(this, new GameRemovedEventArgs(gameId));
        }
    }
}

internal class GameServer(IMapGenerator mapGenerator, ISwoqDatabase database, int? maxNrActiveQuests = null, int? questPollCount = null, TimeSpan? questPollPeriod = null)
    : GameServerBase(mapGenerator, database), IDisposable
{
    private readonly int maxNrActiveQuests = maxNrActiveQuests ?? Parameters.NrOfActiveQuests;
    private readonly int questPollCount = questPollCount ?? Parameters.QuestPollCount;
    private readonly TimeSpan questPollPeriod = questPollPeriod ?? Parameters.QuestPollPeriod;

    private readonly Lock startQuestMutex = new();
    private readonly ConcurrentDictionary<Guid, string> activeQuests = new();
    private readonly AutoResetEvent activeQuestsChanged = new(false);

    public void Dispose()
    {
        activeQuestsChanged.Dispose();
    }

    protected override Game StartTraining(User user, int level, ref int seed)
    {
        CleanupOldGames();

        // Check if user can play this level
        if (level < 0 || level > user.Level || level > mapGenerator.MaxLevel) throw new InvalidLevelException();

        var random = new Random(seed);
        var map = mapGenerator.Generate(level, Parameters.MapHeight, Parameters.MapWidth, random);
        var reporter = new UserStatisticsReporter(user, database);

        // Create new training game
        return new Game(
            map,
            Parameters.MaxTrainingInactivityTime,
            Parameters.MaxLevelTicks,
            Parameters.MaxLevelDuration,
            random,
            reporter);
    }

    protected override Quest StartQuest(User user, ref int seed)
    {
        Debug.Assert(user.Id != null);

        // Cancel any quests the user started earlier
        var activeQuestsByUser = activeQuests.Where(kvp => kvp.Value == user.Id);
        foreach (var activeQuest in activeQuestsByUser)
        {
            if (activeQuests.TryRemove(activeQuest))
            {
                activeQuestsChanged.Set();
            }
            if (games.TryGetValue(activeQuest.Key, out var game))
            {
                game.Cancel();
            }
        }

        for (var i = 0; i < questPollCount; i++)
        {
            CleanupOldGames();

            // We must prevent too many quests starting at the same time.
            // Due to multi-threading, two quests could try starting at the same time, exceeding the limit.
            // Therefore, we lock the whole start process.
            lock (startQuestMutex)
            {
                // Queue (or update entry) user
                questQueue.Enqueue(user);

                // Do not allow starting another quest when too many quests are active.
                // Is this user the first entry in the queue?
                if (activeQuests.Count < maxNrActiveQuests && questQueue.FrontUserId == user.Id)
                {
                    // Dequeue
                    var (frontUserId, frontUserName) = questQueue.Dequeue();
                    Debug.Assert(frontUserId == user.Id);
                    Debug.Assert(frontUserName == user.Name);
                    // and start a new game
                    var quest = new Quest(user, mapGenerator, database, seed);
                    if (activeQuests.TryAdd(quest.Id, user.Id))
                    {
                        activeQuestsChanged.Set();
                    }
                    return quest;
                }
            }

            // Wait a while before checking again, or when the active quests have changed
            activeQuestsChanged.WaitOne(questPollPeriod);
        }
        throw new QuestQueuedException();
    }

    private void CleanupOldGames()
    {
        var finishedGames = games.Values.
            Where(g => g.IsFinished).
            ToList();

        // Remove finished games from active quests list
        var idsFinished = finishedGames.Select(g => g.Id);
        foreach (var id in idsFinished)
        {
            if (activeQuests.TryRemove(id, out _))
            {
                activeQuestsChanged.Set();
            }
        }

        // Remove games that have been finished for a while from the game list
        var now = Clock.Now;
        var idsToRemove = finishedGames.
            Where(g => now - g.LastActionTime > Parameters.GameRetentionTime).
            Select(g => g.Id);
        foreach (var id in idsToRemove)
        {
            RemoveGame(id);
        }
    }
}

internal class FinalGameServer(IMapGenerator mapGenerator, ISwoqDatabase database, IConfiguration config)
    : GameServerBase(mapGenerator, database), IDisposable
{
    private readonly HashSet<string> finalUserIds = (config["final"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet();
    private readonly ConcurrentBag<string> startedUserIds = [];
    private readonly AutoResetEvent questStarted = new(false);

    private readonly int finalSeed = Random.Shared.Next();
    private readonly bool countdownEnabled = config["countdown"] != "no";

    public void Dispose()
    {
        questStarted.Dispose();
    }

    protected override Game StartTraining(User user, int level, ref int seed)
    {
        // Not allowed to start training games during final quest
        throw new NotAllowedException();
    }

    protected override Quest StartQuest(User user, ref int seed)
    {
        Debug.Assert(user.Id != null);

        // Override seed, so all quests use the same seed
        seed = finalSeed;

        // Check that user is in the list of allowed final users
        if (!finalUserIds.Contains(user.Id))
        {
            throw new NotAllowedException();
        }
        // Or has already started
        if (startedUserIds.Contains(user.Id))
        {
            throw new NotAllowedException();
        }

        // Start quest (ignore given seed, use the same seed for all users)
        var quest = new Quest(user, mapGenerator, database, seed);

        // Store in list of active quests
        startedUserIds.Add(user.Id);
        questStarted.Set();

        // Wait until all started
        while (startedUserIds.Count != finalUserIds.Count)
        {
            questStarted.WaitOne(100);
        }

        if (countdownEnabled)
        {
            // Show count down
            for (var i = 5; i > 0; i--)
            {
                Console.WriteLine($"Quest starting for {user.Name} in {i} ...");
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
            Console.WriteLine($"Quest starting for {user.Name} ...");
        }

        return quest;
    }
}
