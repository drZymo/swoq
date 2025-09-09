using Swoq.Data;
using Swoq.Infra;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

internal class FinalGameServer : GameServerBase
{
    private readonly ImmutableHashSet<string> finalUserIds = [];
    private readonly int finalSeed;

    private readonly CancellationTokenSource cancellation = new();
    private readonly Barrier questConnectBarrier;
    private readonly Barrier questStartBarrier;

    private readonly bool countdownEnabled;
    private readonly Task countdownTask;

    private readonly ConcurrentBag<string> startedUserIds = [];

    public FinalGameServer(IMapGenerator mapGenerator, ISwoqDatabase database, IImmutableSet<string> finalUserNames, int? finalSeed = null, bool countdownEnabled = true) : base(mapGenerator, database)
    {
        this.finalUserIds = ToUserIds(database, finalUserNames);
        this.finalSeed = finalSeed ?? Random.Shared.Next();

        // Add 1 to include the countdown task
        questConnectBarrier = new(this.finalUserIds.Count + 1);
        questStartBarrier = new(this.finalUserIds.Count + 1);

        this.countdownEnabled = countdownEnabled;
        countdownTask = Task.Run(Countdown);
    }

    private static ImmutableHashSet<string> ToUserIds(ISwoqDatabase database, IImmutableSet<string> userNames)
    {
        var allUsers = database.GetAllUsers().Result;
        return userNames.
            Select(name => allUsers.SingleOrDefault(u => u.Name == name)?.Id ?? throw new InvalidOperationException($"User {name} does not exist")).
            ToImmutableHashSet();
    }

    public override void Dispose()
    {
        cancellation.Cancel();
        countdownTask.Wait();

        countdownTask.Dispose();
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

            // Signal and wait until all connected
            questConnectBarrier.SignalAndWait(cancellation.Token);

            // Signal and wait until the countdown is finished
            questStartBarrier.SignalAndWait(cancellation.Token);

            // Start quest (use the same seed for all users)
            var quest = new Quest(user, mapGenerator, database, finalSeed);
            return quest;
        }
        catch (OperationCanceledException)
        {
            // Tell the user a quest cannot be started.
            throw new NotAllowedException();
        }
    }

    private void Countdown()
    {
        try
        {
            Console.WriteLine($"{ConsoleColors.BrightYellow}Waiting for players ...{ConsoleColors.Reset}");

            questConnectBarrier.SignalAndWait(cancellation.Token);

            if (countdownEnabled)
            {
                // Show count down
                for (var i = 5; i > 0; i--)
                {
                    Console.WriteLine($"{ConsoleColors.BrightYellow}Final round starting in {i} ...{ConsoleColors.Reset}");
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
                Console.WriteLine($"{ConsoleColors.BrightYellow}Final round starting ...{ConsoleColors.Reset}");
            }

            questStartBarrier.SignalAndWait(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // Exit gracefully
        }
    }
}
