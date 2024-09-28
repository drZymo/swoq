using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Swoq.Interface;
using Swoq.Server.Data;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server.Services;

internal class DashboardService : Interface.DashboardService.DashboardServiceBase, IDisposable
{
    private readonly GameServicePostman gameServicePostman;
    private readonly GameServer gameServer;
    private readonly ISwoqDatabase database;

    private readonly ConcurrentQueue<Update> updates = new();
    private readonly SemaphoreSlim updatesCount = new(0);

    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Thread trainingSessionMonitorThread;

    public DashboardService(GameServicePostman gameServicePostman, GameServer gameServer, ISwoqDatabase database)
    {
        this.gameServicePostman = gameServicePostman;
        this.gameServer = gameServer;
        this.database = database;

        this.gameServicePostman.Started += OnStarted;
        this.gameServicePostman.Acted += OnActed;
        this.gameServer.QueueUpdated += OnQueueUpdated;
        this.gameServer.GameAdded += OnGameAdded;
        this.gameServer.GameRemoved += OnGameRemoved;
        this.gameServer.GameActed += OnGameActed;

        trainingSessionMonitorThread = new Thread(new ThreadStart(TrainingSessionMonitorThread));
        trainingSessionMonitorThread.Start();
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        trainingSessionMonitorThread.Join();

        gameServer.GameActed -= OnGameActed;
        gameServer.GameRemoved -= OnGameRemoved;
        gameServer.GameAdded -= OnGameAdded;
        gameServer.QueueUpdated -= OnQueueUpdated;
        gameServicePostman.Acted -= OnActed;
        gameServicePostman.Started -= OnStarted;
    }

    public override async Task GetUpdates(Empty request, IServerStreamWriter<Update> responseStream, ServerCallContext context)
    {
        try
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await updatesCount.WaitAsync(context.CancellationToken);
                if (updates.TryDequeue(out var update))
                {
                    await responseStream.WriteAsync(update);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }

    public override async Task<Scores> GetScores(Empty request, ServerCallContext context)
    {
        var users = await database.GetAllUsers();

        var scores = new Scores();
        scores.Scores_.AddRange(users.Select(u => new Score()
        {
            UserName = u.Name,
            Level = u.Level,
            LengthTicks = u.QuestLengthTicks,
            LengthSeconds = u.QuestLengthSeconds
        }));
        return scores;
    }

    private Guid currentQuestGameId = Guid.Empty;

    private void OnStarted(object? sender, (string userName, Guid gameId, StartRequest request, StartResponse response) e)
    {
        var isQuest = !e.request.HasLevel;
        if (isQuest)
        {
            currentQuestGameId = e.gameId;

            var update = new Update
            {
                QuestStarted = new QuestStarted
                {
                    GameId = e.gameId.ToString(),
                    UserName = e.userName,
                    Request = e.request,
                    Response = e.response
                }
            };

            updates.Enqueue(update);
            updatesCount.Release();
        }
    }

    private void OnActed(object? sender, (Guid gameId, ActionRequest request, ActionResponse response) e)
    {
        if (e.gameId == currentQuestGameId)
        {
            var update = new Update
            {
                QuestActed = new QuestActed
                {
                    GameId = e.gameId.ToString(),
                    Request = e.request,
                    Response = e.response
                },
            };

            updates.Enqueue(update);
            updatesCount.Release();
        }
    }

    private void OnQueueUpdated(object? sender, IImmutableList<string> queue)
    {
        var update = new Update
        {
            QueueUpdate = new()
        };
        update.QueueUpdate.QueuedUsers.AddRange(queue);

        updates.Enqueue(update);
        updatesCount.Release();
    }


    private void OnGameAdded(object? sender, (Guid gameId, string username, int? level) e)
    {
        gameUpdates.Enqueue(new GameAddedEntry(e.gameId, e.username, e.level));
        gameUpdatesSemaphore.Release();
    }

    private void OnGameRemoved(object? sender, Guid gameId)
    {
        gameUpdates.Enqueue(new GameRemovedEntry(gameId));
        gameUpdatesSemaphore.Release();
    }

    private void OnGameActed(object? sender, (Guid gameId, bool finished) e)
    {
        gameUpdates.Enqueue(new GameActedEntry(e.gameId, e.finished));
        gameUpdatesSemaphore.Release();
    }

    private abstract record GameUpdateEntry(Guid GameId);
    private record GameAddedEntry(Guid GameId, string UserName, int? Level) : GameUpdateEntry(GameId);
    private record GameRemovedEntry(Guid GameId) : GameUpdateEntry(GameId);
    private record GameActedEntry(Guid GameId, bool Finished) : GameUpdateEntry(GameId);

    private readonly SemaphoreSlim gameUpdatesSemaphore = new(0);
    private readonly ConcurrentQueue<GameUpdateEntry> gameUpdates = new();

    private record TrainingSessionEntry(Guid GameId, string UserName, int Level, bool IsActive, bool IsFinished)
    {
        public TrainingSession ToTrainingSession()
        {
            return new TrainingSession
            {
                GameId = GameId.ToString(),
                UserName = UserName,
                Level = Level,
                IsActive = IsActive,
                IsFinished = IsFinished
            };
        }
    }

    private ImmutableDictionary<Guid, TrainingSessionEntry> trainingSessions = ImmutableDictionary<Guid, TrainingSessionEntry>.Empty;

    private void TrainingSessionMonitorThread()
    {
        var lastUpdate = DateTime.MinValue;
        var eventCount = 0;
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                if (gameUpdatesSemaphore.Wait(Parameters.TrainingUpdatePeriod, cancellationTokenSource.Token))
                {
                    gameUpdates.TryDequeue(out var update);
                    switch (update)
                    {
                        case GameAddedEntry e:
                            {
                                if (e.Level.HasValue)
                                {
                                    var session = new TrainingSessionEntry(e.GameId, e.UserName, e.Level.Value, true, false);
                                    trainingSessions = trainingSessions.SetItem(session.GameId, session);
                                }
                            }
                            break;
                        case GameRemovedEntry e:
                            {
                                if (trainingSessions.ContainsKey(e.GameId))
                                {
                                    trainingSessions = trainingSessions.Remove(e.GameId);
                                }
                            }
                            break;
                        case GameActedEntry e:
                            {
                                if (trainingSessions.TryGetValue(e.GameId, out var session))
                                {
                                    session = session with { IsActive = true, IsFinished = e.Finished };
                                    trainingSessions = trainingSessions.SetItem(session.GameId, session);
                                }
                            }
                            break;
                    }
                    eventCount++;
                }

                var now = DateTime.Now;
                var delta = (now - lastUpdate);
                if (delta > Parameters.TrainingUpdatePeriod)
                {
                    var eventsPerSecond = eventCount / (float)delta.TotalSeconds;

                    // Send an updated list
                    var update = new Update
                    {
                        TrainingUpdate = new() { EventsPerSecond = eventsPerSecond }
                    };
                    update.TrainingUpdate.Sessions.AddRange(trainingSessions.Values.Select(s => s.ToTrainingSession()));
                    updates.Enqueue(update);
                    updatesCount.Release();

                    // Mark all sessions as inactive
                    trainingSessions = trainingSessions.ToImmutableDictionary(s => s.Key, s => s.Value with { IsActive = false });

                    // Start waiting another period
                    lastUpdate = now;
                    eventCount = 0;
                }
            }
            catch (OperationCanceledException)
            {
                // Stop gracefully
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }
    }
}
