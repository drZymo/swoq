using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Swoq.Data;
using Swoq.Infra;
using Swoq.Interface;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server.Services;

internal class DashboardService : Interface.DashboardService.DashboardServiceBase, IDisposable
{
    private readonly GameServicePostman gameServicePostman;
    private readonly IGameServer gameServer;
    private readonly ISwoqDatabase database;

    private readonly ConcurrentQueue<Update> updates = new();
    private readonly SemaphoreSlim updatesCount = new(0);

    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Thread sessionMonitorThread;

    private readonly ConcurrentDictionary<Guid, int> activeQuests = new();

    private int activityCount = 0;

    public DashboardService(GameServicePostman gameServicePostman, IGameServer gameServer, ISwoqDatabase database)
    {
        this.gameServicePostman = gameServicePostman;
        this.gameServer = gameServer;
        this.database = database;

        this.gameServicePostman.Started += OnStarted;
        this.gameServicePostman.Acted += OnActed;
        this.gameServer.QueueUpdated += OnQueueUpdated;
        this.gameServer.GameRemoved += OnGameRemoved;
        this.gameServer.GameStatusChanged += OnGameStatusChanged;

        sessionMonitorThread = new Thread(new ThreadStart(SessionMonitorThread));
        sessionMonitorThread.Start();
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        sessionMonitorThread.Join();

        gameServer.GameStatusChanged -= OnGameStatusChanged;
        gameServer.GameRemoved -= OnGameRemoved;
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

    private void OnStarted(object? sender, StartedEventArgs e)
    {
        // Only notify successfully started games
        if (e.Response.Result != StartResult.Ok) return;

        // Send quest request/response data directly to dashboard
        var isQuest = !e.Request.HasLevel;
        if (isQuest)
        {
            var update = new Update
            {
                QuestStarted = new QuestStarted
                {
                    GameId = e.GameId.ToString(),
                    Request = e.Request,
                    Response = e.Response
                }
            };

            updates.Enqueue(update);
            updatesCount.Release();

            // Rembember that this game is a quest
            activeQuests.TryAdd(e.GameId, 1);
        }

        // Send all game info to session update thread
        gameUpdates.Enqueue(new GameAddedEntry(e.GameId, e.Request.UserName, e.Response.State.Level, isQuest));
        gameUpdatesSemaphore.Release();

        // Register activity
        Interlocked.Increment(ref activityCount);
    }

    private void OnActed(object? sender, ActedEventArgs e)
    {
        // Only process succesful actions
        if (e.Response.Result != ActResult.Ok) return;

        // Send quest actions directly to dashboard
        if (activeQuests.ContainsKey(e.GameId))
        {
            var update = new Update
            {
                QuestActed = new QuestActed
                {
                    GameId = e.GameId.ToString(),
                    Request = e.Request,
                    Response = e.Response
                },
            };

            updates.Enqueue(update);
            updatesCount.Release();
        }

        // Send all game info to session update thread
        gameUpdates.Enqueue(new GameUpdatedEntry(e.GameId, e.Response.State.Level, e.Response.State.Status != GameStatus.Active));
        gameUpdatesSemaphore.Release();

        // Register activity
        Interlocked.Increment(ref activityCount);
    }

    private void OnQueueUpdated(object? sender, QueueUpdatedEventArgs e)
    {
        var update = new Update
        {
            QueueUpdate = new()
        };
        update.QueueUpdate.QueuedUsers.AddRange(e.QueuedUsers);

        updates.Enqueue(update);
        updatesCount.Release();
    }

    private void OnGameRemoved(object? sender, GameRemovedEventArgs e)
    {
        gameUpdates.Enqueue(new GameRemovedEntry(e.GameId));
        gameUpdatesSemaphore.Release();
    }

    private void OnGameStatusChanged(object? sender, GameStatusChangedEventArgs e)
    {
        // Send quest actions directly to dashboard
        if (activeQuests.ContainsKey(e.GameId))
        {
            var update = new Update
            {
                QuestStatusChanged = new QuestStatusChanged
                {
                    GameId = e.GameId.ToString(),
                    Status = e.Status
                },
            };

            updates.Enqueue(update);
            updatesCount.Release();
        }

        gameUpdates.Enqueue(new GameStatusChangedEntry(e.GameId, e.Status));
        gameUpdatesSemaphore.Release();
    }

    private abstract record GameUpdateEntry(Guid GameId);
    private record GameAddedEntry(Guid GameId, string UserName, int Level, bool IsQuest) : GameUpdateEntry(GameId);
    private record GameUpdatedEntry(Guid GameId, int Level, bool IsFinished) : GameUpdateEntry(GameId);
    private record GameRemovedEntry(Guid GameId) : GameUpdateEntry(GameId);
    private record GameStatusChangedEntry(Guid GameId, GameStatus Status) : GameUpdateEntry(GameId);

    private readonly SemaphoreSlim gameUpdatesSemaphore = new(0);
    private readonly ConcurrentQueue<GameUpdateEntry> gameUpdates = new();

    private record SessionEntry(Guid GameId, string UserName, int Level, bool IsQuest, bool IsActive, bool IsFinished)
    {
        public Session ToSession()
        {
            return new Session
            {
                GameId = GameId.ToString(),
                UserName = UserName,
                Level = Level,
                IsQuest = IsQuest,
                IsActive = IsActive,
                IsFinished = IsFinished
            };
        }
    }

    private void SessionMonitorThread()
    {
        var sessions = ImmutableDictionary<Guid, SessionEntry>.Empty;
        var lastSessionsUpdate = DateTime.MinValue;
        var lastScoresUpdate = DateTime.MinValue;
        var lastStatisticsUpdate = DateTime.MinValue;

        while (!cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                if (gameUpdatesSemaphore.Wait(TimeSpan.FromMilliseconds(100), cancellationTokenSource.Token))
                {
                    gameUpdates.TryDequeue(out var update);
                    switch (update)
                    {
                        case GameAddedEntry e:
                            {
                                // Add to session list
                                var session = new SessionEntry(e.GameId, e.UserName, e.Level, e.IsQuest, true, false);
                                sessions = sessions.SetItem(session.GameId, session);
                            }
                            break;
                        case GameRemovedEntry e:
                            {
                                // Remove from active quests so data is no longer transmitted
                                activeQuests.TryRemove(e.GameId, out _);

                                // Remove from session list
                                sessions = sessions.Remove(e.GameId);
                            }
                            break;
                        case GameUpdatedEntry e:
                            {
                                // Mark as active
                                if (sessions.TryGetValue(e.GameId, out var session))
                                {
                                    session = session with { Level = e.Level, IsActive = true, IsFinished = e.IsFinished };
                                    sessions = sessions.SetItem(session.GameId, session);
                                }
                            }
                            break;
                        case GameStatusChangedEntry e:
                            {
                                if (sessions.TryGetValue(e.GameId, out var session))
                                {
                                    session = session with { IsFinished = e.Status != GameStatus.Active };
                                    sessions = sessions.SetItem(session.GameId, session);
                                }
                            }
                            break;
                    }
                }

                SendSessionsUpdateIfNeeded(ref sessions, ref lastSessionsUpdate);
                SendScoresUpdateIfNeeded(ref lastScoresUpdate);
                SendStatisticsIfNeeded(ref lastStatisticsUpdate);
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

    private void SendSessionsUpdateIfNeeded(ref ImmutableDictionary<Guid, SessionEntry> sessions, ref DateTime lastSessionsUpdate)
    {
        var now = Clock.Now;
        if (now - lastSessionsUpdate < Parameters.SessionsUpdatePeriod) return;

        // Send an updated list
        var update = new Update
        {
            SessionsUpdate = new()
        };
        update.SessionsUpdate.Sessions.AddRange(sessions.Values.Select(s => s.ToSession()));
        updates.Enqueue(update);
        updatesCount.Release();

        // Mark all sessions as inactive
        sessions = sessions.ToImmutableDictionary(s => s.Key, s => s.Value with { IsActive = false });

        // Start waiting another period
        lastSessionsUpdate = now;
    }

    private void SendScoresUpdateIfNeeded(ref DateTime lastScoresUpdate)
    {
        var now = Clock.Now;
        if (now - lastScoresUpdate < Parameters.ScoresUpdatePeriod) return;

        // Get scores from database
        var users = database.GetAllUsers().Result;
        var scores = users.Select(u => new Score()
        {
            UserName = u.Name,
            Level = u.Level,
            LengthTicks = u.QuestLengthTicks,
            LengthSeconds = u.QuestLengthSeconds,
            QuestFinished = u.QuestFinished,
        });

        // Send update
        var update = new Update
        {
            ScoresUpdate = new()
        };
        update.ScoresUpdate.Scores.AddRange(scores);
        updates.Enqueue(update);
        updatesCount.Release();

        // Start waiting another period
        lastScoresUpdate = now;
    }

    private void SendStatisticsIfNeeded(ref DateTime lastStatisticsUpdate)
    {
        var now = Clock.Now;
        var delta = now - lastStatisticsUpdate;
        if (delta < Parameters.StatisticsUpdatePeriod) return;

        // Get and reset info
        var activityCount = Interlocked.Exchange(ref this.activityCount, 0);

        // Compute statistics
        var eventsPerSecond = activityCount / (float)delta.TotalSeconds;

        // Send update
        var update = new Update
        {
            StatisticsUpdate = new() { EventsPerSecond = eventsPerSecond },
        };
        updates.Enqueue(update);
        updatesCount.Release();

        // Start waiting another period
        lastStatisticsUpdate = now;
    }
}
