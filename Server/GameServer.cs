using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;
using System.Collections.Concurrent;
using System.Collections.Immutable;
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

    public event EventHandler<GameRemovedEventArgs>? GameRemoved;

    public event EventHandler<QueueUpdatedEventArgs>? QueueUpdated;

    public GameStartResult Start(string userId, string userName, int? level, int? seed = null)
    {
        try
        {
            var user = GetUserOrThrow(database, userId, userName);

            // If seed is not given, use a random one.
            var actualSeed = seed ?? Random.Shared.Next();

            // Cleanup
            CleanupOldGames();

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
                var quest = StartQuest(user);
                actualSeed = quest.Seed;
                game = quest;
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

    private static User GetUserOrThrow(ISwoqDatabase database, string userId, string userName)
    {
        try
        {
            return database.FindUserAsync(userId, userName).Result ?? throw new UnknownUserException();
        }
        catch
        {
            throw new UnknownUserException();
        }
    }

    protected abstract Game StartTraining(User user, int level, ref int seed);

    protected abstract Quest StartQuest(User user);

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

    private void CleanupOldGames()
    {
        // Remove games that have been finished for a while from the game list
        var now = Clock.Now;
        var idsToRemove = games.Values.
            Where(g => g.IsFinished && (now - g.LastActionTime) > Parameters.GameRetentionTime).
            Select(g => g.Id).
            ToList();
        foreach (var id in idsToRemove)
        {
            RemoveGame(id);
        }
    }

    protected void RemoveGame(Guid gameId)
    {
        if (games.TryRemove(gameId, out var game))
        {
            GameRemoved?.Invoke(this, new GameRemovedEventArgs(gameId));
        }
    }

    protected void OnQueueUpdated(IImmutableList<string> queuedUsers)
    {
        QueueUpdated?.Invoke(this, new QueueUpdatedEventArgs(queuedUsers));
    }
}

internal class GameServer : GameServerBase, IDisposable
{
    private readonly int maxNrActiveQuests;
    private readonly TimeSpan queueWaitTime;

    private readonly CancellationTokenSource cancel = new();
    private readonly Thread queueManagerThread;

    public GameServer(IMapGenerator mapGenerator, ISwoqDatabase database, int? maxNrActiveQuests = null, TimeSpan? queueWaitTime = null) : base(mapGenerator, database)
    {
        this.maxNrActiveQuests = maxNrActiveQuests ?? Parameters.NrOfActiveQuests;
        this.queueWaitTime = queueWaitTime ?? Parameters.QueueWaitTime;

        queueManagerThread = new Thread(new ThreadStart(QueueManagerThread));
        queueManagerThread.Start();
    }

    public void Dispose()
    {
        cancel.Cancel();
        queueManagerThread.Join();
    }


    private abstract record QueueRequest(User User);
    private record QueueWaitBeginRequest(User User, ManualResetEventSlim WaitEvent) : QueueRequest(User);
    private record QueueWaitEndRequest(User User, TaskCompletionSource<Quest?> CompletionSource) : QueueRequest(User);

    private readonly SemaphoreSlim requestsSemaphore = new(0);
    private readonly ConcurrentQueue<QueueRequest> requests = new();

    private void SendQueueRequest(QueueRequest request)
    {
        requests.Enqueue(request);
        requestsSemaphore.Release();
    }

    private record QueueEntry(string UserId, string UserName, ManualResetEventSlim? WaitEvent, DateTime LastUpdateTime)
    {
        public static readonly QueueEntry None = new("", "", null, DateTime.MinValue);
    }

    private void QueueManagerThread()
    {
        Dictionary<string, Quest> activeQuests = [];
        Dictionary<string, QueueEntry> entries = [];
        List<string> queue = [];

        void SendQueueUpdate()
        {
            var queuedUsers = queue.Select(id => entries.GetValueOrDefault(id, QueueEntry.None).UserName).ToImmutableArray();
            OnQueueUpdated(queuedUsers);
        }

        try
        {
            while (!cancel.IsCancellationRequested)
            {
                requestsSemaphore.Wait(Parameters.QueuePollPeriod, cancel.Token);

                if (requests.TryDequeue(out var request))
                {
                    switch (request)
                    {
                        case QueueWaitBeginRequest begin:
                            {
                                Debug.Assert(begin.User.Id != null);
                                // Stop any active quests of this user
                                if (activeQuests.Remove(begin.User.Id, out var activeQuest))
                                {
                                    if (games.TryGetValue(activeQuest.Id, out var game))
                                    {
                                        game.Cancel();
                                    }
                                }

                                // Update entry in queue
                                if (entries.TryGetValue(begin.User.Id, out var entry))
                                {
                                    entry = entry with { WaitEvent = begin.WaitEvent, LastUpdateTime = Clock.Now };
                                    entries[entry.UserId] = entry;
                                    Debug.Assert(queue.Contains(entry.UserId));
                                }
                                else
                                {
                                    entry = new QueueEntry(begin.User.Id, begin.User.Name, begin.WaitEvent, Clock.Now);

                                    // Add the end of queue
                                    entries.Add(entry.UserId, entry);
                                    queue.Add(entry.UserId);
                                    SendQueueUpdate();
                                }
                            }
                            break;

                        case QueueWaitEndRequest end:
                            {
                                Debug.Assert(end.User.Id != null);
                                Quest? quest = null;
                                if (entries.TryGetValue(end.User.Id, out var entry))
                                {
                                    // Update entry
                                    entry = entry with { WaitEvent = null, LastUpdateTime = Clock.Now };
                                    entries[entry.UserId] = entry;

                                    // Can we create a new active quest and is it the first in the queue?
                                    if (activeQuests.Count < maxNrActiveQuests && queue.Count > 0 && queue[0] == entry.UserId)
                                    {
                                        // Start a new game
                                        var seed = Random.Shared.Next();
                                        quest = new Quest(end.User, mapGenerator, database, seed);
                                        activeQuests.Add(entry.UserId, quest);

                                        // Remove from queue
                                        queue.RemoveAt(0);
                                        entries.Remove(entry.UserId);
                                        SendQueueUpdate();
                                    }
                                }
                                // Notify result
                                end.CompletionSource.SetResult(quest);
                            }
                            break;
                    }
                }

                // Remove stale entries
                var now = Clock.Now;
                var staleEntries = entries.Values.Where(e => (now - e.LastUpdateTime) > Parameters.MaxQuestInactivityTime).ToList();
                foreach (var entry in staleEntries)
                {
                    entries.Remove(entry.UserId);
                    queue.Remove(entry.UserId);
                    SendQueueUpdate();
                }

                // Remove finished active quests
                var finishedUserIds = activeQuests.Where(kvp => kvp.Value.IsFinished).Select(kvp => kvp.Key).ToList();
                foreach (var userId in finishedUserIds)
                {
                    activeQuests.Remove(userId);
                }

                if (activeQuests.Count < maxNrActiveQuests && queue.Count > 0)
                {
                    // Unblock first in queue
                    var firstUserId = queue.First();
                    if (entries.TryGetValue(firstUserId, out var firstEntry))
                    {
                        // Unblock waiter (if any)
                        // It will follow with a WaitEndRequest where we can provide it with a new Quest
                        firstEntry.WaitEvent?.Set();
                    }
                    else
                    {
                        // Inconsistency, should not happen.
                        // Just drop the first user in the queue and try next entry.
                        queue.RemoveAt(0);
                        SendQueueUpdate();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // exit gracefully
        }
    }

    protected override Game StartTraining(User user, int level, ref int seed)
    {
        // Check if user can play this level
        if (level < 0 || level > user.Level || level > mapGenerator.MaxLevel) throw new InvalidLevelException();

        var random = new Random(seed + level);
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

    protected override Quest StartQuest(User user)
    {
        Debug.Assert(user.Id != null);

        // Start waiting to be at the top
        ManualResetEventSlim waitEvent = new();
        SendQueueRequest(new QueueWaitBeginRequest(user, waitEvent));
        waitEvent.Wait(queueWaitTime);

        // Stop waiting and get result
        TaskCompletionSource<Quest?> result = new();
        SendQueueRequest(new QueueWaitEndRequest(user, result));
        var quest = result.Task.Result;
        return quest ?? throw new QuestQueuedException();
    }

}

internal class FinalGameServer : GameServerBase, IDisposable
{
    private readonly HashSet<string> finalUserIds;
    private readonly ConcurrentBag<string> startedUserIds = [];
    private readonly Barrier questStartBarrier;

    private readonly int finalSeed;
    private readonly bool countdownEnabled;

    public FinalGameServer(IMapGenerator mapGenerator, ISwoqDatabase database, IConfiguration config, int? finalSeed = null) : base(mapGenerator, database)
    {
        finalUserIds = (config["final"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet();
        this.finalSeed = finalSeed ?? Random.Shared.Next();
        countdownEnabled = config["countdown"] != "no";

        questStartBarrier = new(finalUserIds.Count);
    }

    public void Dispose()
    {
    }

    protected override Game StartTraining(User user, int level, ref int seed)
    {
        // Not allowed to start training games during final quest
        throw new NotAllowedException();
    }

    protected override Quest StartQuest(User user)
    {
        Debug.Assert(user.Id != null);

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
        startedUserIds.Add(user.Id);

        // Signal and wait until all started
        questStartBarrier.SignalAndWait();

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

        // Start quest (use the same seed for all users)
        var quest = new Quest(user, mapGenerator, database, finalSeed);
        return quest;
    }
}
