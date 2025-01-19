using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Swoq.Data;
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

    public DashboardService(GameServicePostman gameServicePostman, IGameServer gameServer, ISwoqDatabase database)
    {
        this.gameServicePostman = gameServicePostman;
        this.gameServer = gameServer;
        this.database = database;

        this.gameServicePostman.Started += OnStarted;
        this.gameServicePostman.Acted += OnActed;
        this.gameServer.QueueUpdated += OnQueueUpdated;
        this.gameServer.GameAdded += OnGameAdded;
        this.gameServer.GameRemoved += OnGameRemoved;
        this.gameServer.GameUpdated += OnGameUpdated;
        this.gameServer.StatisticsUpdated += OnStatisticsUpdated;

        sessionMonitorThread = new Thread(new ThreadStart(SessionMonitorThread));
        sessionMonitorThread.Start();
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        sessionMonitorThread.Join();

        gameServer.StatisticsUpdated -= OnStatisticsUpdated;
        gameServer.GameUpdated -= OnGameUpdated;
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


    private void OnGameAdded(object? sender, GameAddedEventArgs e)
    {
        gameUpdates.Enqueue(new GameAddedEntry(e));
        gameUpdatesSemaphore.Release();
    }

    private void OnGameRemoved(object? sender, GameRemovedEventArgs e)
    {
        gameUpdates.Enqueue(new GameRemovedEntry(e));
        gameUpdatesSemaphore.Release();
    }

    private void OnGameUpdated(object? sender, GameUpdatedEventArgs e)
    {
        gameUpdates.Enqueue(new GameUpdatedEntry(e));
        gameUpdatesSemaphore.Release();
    }

    private void OnStatisticsUpdated(object? sender, StatisticsUpdatedEventArgs e)
    {
        var update = new Update
        {
            StatisticsUpdate = new() { EventsPerSecond = e.EventsPerSecond },
        };
        updates.Enqueue(update);
        updatesCount.Release();
    }

    private abstract record GameUpdateEntry(Guid GameId);
    private record GameAddedEntry(GameAddedEventArgs Args) : GameUpdateEntry(Args.GameId);
    private record GameRemovedEntry(GameRemovedEventArgs Args) : GameUpdateEntry(Args.GameId);
    private record GameUpdatedEntry(GameUpdatedEventArgs Args) : GameUpdateEntry(Args.GameId);

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

    private ImmutableDictionary<Guid, SessionEntry> sessions = ImmutableDictionary<Guid, SessionEntry>.Empty;
    private DateTime lastSessionsUpdate = DateTime.MinValue;
    private DateTime lastScoresUpdate = DateTime.MinValue;

    private void SessionMonitorThread()
    {
        sessions = ImmutableDictionary<Guid, SessionEntry>.Empty;
        lastSessionsUpdate = DateTime.MinValue;
        lastScoresUpdate = DateTime.MinValue;

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
                                var session = new SessionEntry(e.Args.GameId, e.Args.UserName, e.Args.Level, e.Args.IsQuest, true, false);
                                sessions = sessions.SetItem(session.GameId, session);
                            }
                            break;
                        case GameRemovedEntry e:
                            {
                                if (sessions.ContainsKey(e.Args.GameId))
                                {
                                    sessions = sessions.Remove(e.GameId);
                                }
                            }
                            break;
                        case GameUpdatedEntry e:
                            {
                                if (sessions.TryGetValue(e.Args.GameId, out var session))
                                {
                                    session = session with { Level = e.Args.Level, IsActive = true, IsFinished = e.Args.IsFinished };
                                    sessions = sessions.SetItem(session.GameId, session);
                                }
                            }
                            break;
                    }
                }

                var now = DateTime.Now;

                SendSessionsUpdateIfNeeded(now);
                SendScoresUpdateIfNeeded(now);
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

    private void SendSessionsUpdateIfNeeded(DateTime now)
    {
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

    private async void SendScoresUpdateIfNeeded(DateTime now)
    {
        if (now - lastScoresUpdate < Parameters.ScoresUpdatePeriod) return;

        // Get scores from database
        var users = await database.GetAllUsers();
        var scores = users.Select(u => new Score()
        {
            UserName = u.Name,
            Level = u.Level,
            LengthTicks = u.QuestLengthTicks,
            LengthSeconds = u.QuestLengthSeconds
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
}
