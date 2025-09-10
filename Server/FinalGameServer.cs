using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

internal class FinalGameServer : GameServerBase
{
    private readonly ILogger? logger;
    private readonly ImmutableHashSet<string> finalUserIds = [];
    private readonly int finalSeed;

    private readonly CancellationTokenSource cancellation = new();
    private readonly Barrier questConnectBarrier;
    private readonly Barrier questStartBarrier;

    private readonly bool countdownEnabled;
    private readonly Task coordinatorThread;

    private readonly ConcurrentBag<string> startedUserIds = [];
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> tickers = [];

    public FinalGameServer(IMapGenerator mapGenerator, ISwoqDatabase database, ILogger<FinalGameServer>? logger, IImmutableSet<string> finalUserNames, int? finalSeed = null, bool countdownEnabled = true) : base(mapGenerator, database)
    {
        this.finalUserIds = ToUserIds(database, finalUserNames);
        this.logger = logger;
        this.finalSeed = finalSeed ?? Random.Shared.Next();

        // Add 1 to include the countdown task
        questConnectBarrier = new(this.finalUserIds.Count + 1);
        questStartBarrier = new(this.finalUserIds.Count + 1);

        this.countdownEnabled = countdownEnabled;
        coordinatorThread = Task.Run(CoordinatorThread);
    }

    public override void Dispose()
    {
        cancellation.Cancel();
        coordinatorThread.Wait();

        coordinatorThread.Dispose();
        questConnectBarrier.Dispose();
        questStartBarrier.Dispose();

        base.Dispose();
    }

    protected override Game StartTraining(User user, int level, ref int seed)
    {
        // Not allowed to start training games during final quest
        throw new NotAllowedException();
    }

    protected override Quest StartQuest(User user)
    {
        try
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

            logger?.LogInformation("User {Name} connected", user.Name);

            // Signal and wait until all connected
            questConnectBarrier.SignalAndWait(cancellation.Token);

            // Signal and wait until the countdown is finished
            questStartBarrier.SignalAndWait(cancellation.Token);

            // Start quest (use the same seed for all users)
            var ticker = new SemaphoreSlim(0);
            var quest = new Quest(user, mapGenerator, database, finalSeed);
            tickers.AddOrUpdate(quest.Id, ticker, (k, v) => ticker);

            return quest;
        }
        catch (OperationCanceledException)
        {
            // Tell the user a quest cannot be started.
            throw new NotAllowedException();
        }
    }

    public override GameState Act(Guid gameId, DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        // Wait for next tick
        if (tickers.TryGetValue(gameId, out var ticker))
        {
            ticker.Wait();
        }
        return base.Act(gameId, action1, action2);
    }

    private void CoordinatorThread()
    {
        try
        {
            logger?.LogWarning("Waiting for players!");

            // Wait until all connected
            questConnectBarrier.SignalAndWait(cancellation.Token);

            // Start count down
            if (countdownEnabled)
            {
                // Show count down
                for (var i = 5; i > 0; i--)
                {
                    logger?.LogCritical("Final round starting in {Remaining} ...", i);
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
                logger?.LogCritical("Final round starting !");
            }

            // Signal countdown finished, so games are started
            questStartBarrier.SignalAndWait(cancellation.Token);

            // Increment all tickers periodically
            var delayUs = 1000000 / Parameters.FinalTickRate;
            while (!cancellation.IsCancellationRequested)
            {
                Thread.Sleep(TimeSpan.FromMicroseconds(delayUs));
                foreach (var ticker in tickers.Values)
                {
                    ticker.Release();
                }
            }

        }
        catch (OperationCanceledException)
        {
            // Exit gracefully
        }
    }

    private static ImmutableHashSet<string> ToUserIds(ISwoqDatabase database, IImmutableSet<string> userNames)
    {
        var allUsers = database.GetAllUsers().Result;
        return userNames.
            Select(name => allUsers.SingleOrDefault(u => u.Name == name)?.Id ?? throw new InvalidOperationException($"User {name} does not exist")).
            ToImmutableHashSet();
    }
}
