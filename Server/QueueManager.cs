using Swoq.Data;
using Swoq.Infra;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

internal class QueueManager : IDisposable
{
    private readonly IMapGenerator mapGenerator;
    private readonly ISwoqDatabase database;
    private readonly int maxNrActiveQuests;
    private readonly TimeSpan queueWaitTime;

    private readonly CancellationTokenSource cancellation = new();
    private readonly Thread queueManagerThread;

    public QueueManager(
        IMapGenerator mapGenerator,
        ISwoqDatabase database,
        int? maxNrActiveQuests = null,
        TimeSpan? queueWaitTime = null)
    {
        this.mapGenerator = mapGenerator;
        this.database = database;
        this.maxNrActiveQuests = maxNrActiveQuests ?? Parameters.NrOfActiveQuests;
        this.queueWaitTime = queueWaitTime ?? Parameters.QueueWaitTime;

        queueManagerThread = new Thread(new ThreadStart(QueueManagerThread));
        queueManagerThread.Start();
    }

    public void Dispose()
    {
        cancellation.Cancel();
        queueManagerThread.Join();
        requestsSemaphore.Dispose();
    }

    public event EventHandler<QueueUpdatedEventArgs>? QueueUpdated;
    public event EventHandler<GameStatusChangedEventArgs>? GameStatusChanged;

    public Quest? TryStartQuest(User user)
    {
        // Start waiting to be at the top
        using ManualResetEventSlim waitEvent = new();
        SendQueueRequest(new QueueWaitBeginRequest(user, waitEvent));
        waitEvent.Wait(queueWaitTime, cancellation.Token);

        // Stop waiting and get result
        TaskCompletionSource<Quest?> result = new();
        SendQueueRequest(new QueueWaitEndRequest(user, result));
        result.Task.Wait(cancellation.Token);
        var quest = result.Task.Result;
        return quest;
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

    private readonly Dictionary<string, Quest> activeQuests = [];
    private readonly Dictionary<string, QueueEntry> entries = [];
    private readonly List<string> queue = [];

    private void QueueManagerThread()
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                requestsSemaphore.Wait(Parameters.QueuePollPeriod, cancellation.Token);

                if (requests.TryDequeue(out var request))
                {
                    switch (request)
                    {
                        case QueueWaitBeginRequest begin:
                            HandleQueueWaitBegin(begin);
                            break;

                        case QueueWaitEndRequest end:
                            HandleQueueWaitEnd(end);
                            break;
                    }
                }

                RemoveStaleEntries();

                RemoveFinishedActiveQuests();

                SignalFirstInQueue();
            }
        }
        catch (OperationCanceledException)
        {
            // exit gracefully
        }
    }
    private void HandleQueueWaitBegin(QueueWaitBeginRequest begin)
    {
        Debug.Assert(begin.User.Id != null);
        // Stop any active quests of this user
        if (activeQuests.Remove(begin.User.Id, out var activeQuest))
        {
            if (!activeQuest.IsFinished)
            {
                activeQuest.Cancel();
                OnGameStatusChanged(activeQuest);
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
            // Add to the end of queue
            entry = new QueueEntry(begin.User.Id, begin.User.Name, begin.WaitEvent, Clock.Now);
            Enqueue(entry);
        }
    }

    private void HandleQueueWaitEnd(QueueWaitEndRequest end)
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

                RemoveFromQueue(entry.UserId);
            }
        }
        // Notify result
        end.CompletionSource.SetResult(quest);
    }

    private void RemoveStaleEntries()
    {
        var now = Clock.Now;
        var staleEntries = entries.Values.Where(e => (now - e.LastUpdateTime) > Parameters.MaxQuestInactivityTime).ToList();
        foreach (var entry in staleEntries)
        {
            RemoveFromQueue(entry.UserId);
        }
    }

    private void RemoveFinishedActiveQuests()
    {
        var finishedUserIds = activeQuests.Where(kvp => kvp.Value.IsFinished).Select(kvp => kvp.Key).ToList();
        foreach (var userId in finishedUserIds)
        {
            if (activeQuests.Remove(userId, out var quest))
            {
                OnGameStatusChanged(quest);
            }
        }
    }

    private void SignalFirstInQueue()
    {
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
                // Just drop this user from the queue and try next entry.
                RemoveFromQueue(firstUserId);
            }
        }
    }

    void Enqueue(QueueEntry entry)
    {
        entries.Add(entry.UserId, entry);
        queue.Add(entry.UserId);
        SendQueueUpdate();
    }

    void RemoveFromQueue(string userId)
    {
        queue.Remove(userId);
        entries.Remove(userId);
        SendQueueUpdate();
    }

    void SendQueueUpdate()
    {
        var queuedUsers = queue.Select(id => entries.GetValueOrDefault(id, QueueEntry.None).UserName).ToImmutableArray();
        QueueUpdated?.Invoke(this, new QueueUpdatedEventArgs(queuedUsers));
    }

    protected void OnGameStatusChanged(Quest quest)
    {
        GameStatusChanged?.Invoke(this, new GameStatusChangedEventArgs(quest.Id, quest.State.Status));
    }
}
