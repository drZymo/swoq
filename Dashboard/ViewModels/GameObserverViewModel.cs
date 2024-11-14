using Avalonia.Threading;
using Grpc.Net.Client;
using Swoq.InfraUI.Models;
using Swoq.InfraUI.ViewModels;
using Swoq.Interface;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Swoq.Dashboard.ViewModels;

internal class GameObserverViewModel : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Thread getUpdatesThread;

    public GameObserverViewModel()
    {
        GameObservations = new(gameObservations);

        QueueUsers = new(queuedUsers);
        Sessions = new(sessions);
        Scores = new(scores);

        getUpdatesThread = new Thread(new ThreadStart(GetUpdatesThread));
        getUpdatesThread.Start();
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        getUpdatesThread.Join();
    }

    private ImmutableDictionary<string, GameObservationViewModel> gameObservationIds = ImmutableDictionary<string, GameObservationViewModel>.Empty;

    private readonly ObservableCollection<GameObservationViewModel> gameObservations = new();
    public ReadOnlyObservableCollection<GameObservationViewModel> GameObservations { get; private set; }

    private GameObservationViewModel gameObservation = new();
    public GameObservationViewModel GameObservation
    {
        get => gameObservation;
        private set
        {
            gameObservation = value;
            OnPropertyChanged();
        }
    }

    private readonly ObservableCollection<string> queuedUsers = [];
    public ReadOnlyObservableCollection<string> QueueUsers { get; }

    private readonly ObservableCollection<SessionViewModel> sessions = [];
    public ReadOnlyObservableCollection<SessionViewModel> Sessions { get; }

    public record Score(string UserName, int Level, int LengthTicks, int LengthSeconds);

    private readonly ObservableCollection<Score> scores = [];
    public ReadOnlyObservableCollection<Score> Scores { get; }


    private string statusMessage = "";
    public string StatusMessage
    {
        get => statusMessage;
        private set
        {
            statusMessage = value;
            OnPropertyChanged();
        }
    }

    private double eventsPerSecond = 0;
    public double EventsPerSecond
    {
        get => eventsPerSecond;
        private set
        {
            eventsPerSecond = value;
            OnPropertyChanged();
        }
    }

    private async void GetUpdatesThread()
    {
        var callOptions = new Grpc.Core.CallOptions(cancellationToken: cancellationTokenSource.Token);
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                bool connected = false;
                Dispatcher.UIThread.Invoke(() => { StatusMessage = "Connecting..."; });
                using var channel = GrpcChannel.ForAddress("http://localhost:5080");
                var client = new DashboardService.DashboardServiceClient(channel);

                GameObservationBuilder? gameStateBuilder = null;

                var call = client.GetUpdates(new Google.Protobuf.WellKnownTypes.Empty(), callOptions);
                while (await call.ResponseStream.MoveNext(cancellationTokenSource.Token))
                {
                    if (!connected)
                    {
                        Dispatcher.UIThread.Invoke(() => { StatusMessage = "Connected"; });
                        connected = true;
                    }

                    var message = call.ResponseStream.Current;

                    if (message.QuestStarted != null)
                    {
                        gameStateBuilder = HandleQuestStarted(message.QuestStarted);
                    }

                    if (message.QuestActed != null && gameStateBuilder != null)
                    {
                        if (gameStateBuilder.GameId == message.QuestActed.GameId)
                        {
                            HandleQuestActed(message.QuestActed, gameStateBuilder);
                        }
                    }

                    if (message.QueueUpdate != null)
                    {
                        var update = message.QueueUpdate;
                        HandleQueue(update);
                    }

                    if (message.SessionsUpdate != null)
                    {
                        UpdateSessions(message.SessionsUpdate);
                    }

                    if (message.ScoresUpdate != null)
                    {
                        UpdateScores(message.ScoresUpdate);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Stop gracefully
                break;
            }
            catch (Grpc.Core.RpcException ex)
            {
                if (ex.InnerException is OperationCanceledException)
                {
                    // Stop gracefully on cancel
                    break;
                }
                Dispatcher.UIThread.Invoke(() => { StatusMessage = "Disconnected"; });
                Debug.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Invoke(() => { StatusMessage = "Internal error"; });
                Debug.WriteLine($"Exception {ex.GetType()}: {ex.Message}");
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }
    }

    private GameObservationBuilder HandleQuestStarted(QuestStarted update)
    {
        var gameStateBuilder = new GameObservationBuilder(update.GameId, update.Response.Height, update.Response.Width, update.Response.VisibilityRange, update.UserName);
        var gameState = gameStateBuilder.BuildNext(null, update.Response.State, update.Response.Result, Dispatcher.UIThread);
        Dispatcher.UIThread.Invoke(() =>
        {
            var gameObservationViewModel = new GameObservationViewModel(gameState);
            gameObservations.Add(gameObservationViewModel);
            gameObservationIds = gameObservationIds.Add(update.GameId, gameObservationViewModel);
        });
        return gameStateBuilder;
    }

    private void HandleQuestActed(QuestActed update, GameObservationBuilder gameStateBuilder)
    {
        if (gameObservationIds.TryGetValue(update.GameId, out var gameObservationViewModel))
        {
            var gameState = gameStateBuilder.BuildNext(update.Request, update.Response.State, update.Response.Result, Dispatcher.UIThread);
            Dispatcher.UIThread.Invoke(() => { gameObservationViewModel.SetGameObservation(gameState); });
        }
    }

    private void HandleQueue(QueueUpdate update)
    {
        var queuedUsers = update.QueuedUsers.ToImmutableArray();
        Dispatcher.UIThread.Invoke(() =>
        {
            this.queuedUsers.Clear();
            foreach (var qu in queuedUsers)
            {
                this.queuedUsers.Add(qu);
            }
        });
    }

    private void UpdateSessions(SessionsUpdate update)
    {
        var newSessions = update.Sessions;

        // Update events per second
        EventsPerSecond = update.EventsPerSecond;

        // Find out which game ids have been added, removed and updated
        var newIds = newSessions.Select(s => s.GameId).ToImmutableHashSet();
        var oldIds = sessions.Select(s => s.Id).ToImmutableHashSet();

        var addedSessionIds = newIds.Except(oldIds);
        var removedSessionIds = oldIds.Except(newIds);
        var updatedSessionIds = newIds.Intersect(newIds);

        // Remove sessions
        var sessionsToRemove = sessions.Where(s => removedSessionIds.Contains(s.Id)).ToImmutableArray();
        Dispatcher.UIThread.Invoke(() =>
        {
            foreach (var session in sessionsToRemove)
            {
                sessions.Remove(session);
            }
        });

        // TODO: Remove game observations with this id


        // Add sessions at the start
        var sessionsToAdd = newSessions.Where(s => addedSessionIds.Contains(s.GameId)).ToImmutableArray();
        Dispatcher.UIThread.Invoke(() =>
        {
            foreach (var session in sessionsToAdd)
            {
                sessions.Insert(
                    0,
                    new SessionViewModel(
                        session.GameId,
                        session.UserName,
                        session.Level,
                        session.IsQuest,
                        session.IsActive,
                        session.IsFinished));
            }
        });

        // Update existing
        foreach (var id in updatedSessionIds)
        {
            var newSession = newSessions.Where(s => s.GameId == id).FirstOrDefault();
            var oldSession = sessions.Where(s => s.Id == id).FirstOrDefault();

            if (newSession != null && oldSession != null)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    oldSession.Level = newSession.Level;
                    oldSession.IsActive = newSession.IsActive;
                    oldSession.IsFinished = newSession.IsFinished;
                });
            }
        }
    }

    private void UpdateScores(ScoresUpdate update)
    {
        var orderedScores = update.Scores.
            Select(s => new Score(s.UserName, s.Level, s.LengthTicks, s.LengthSeconds)).
            OrderByDescending(s => s.Level).
            ThenBy(s => s.LengthTicks).
            ThenBy(s => s.LengthSeconds).
            ToImmutableArray();

        Dispatcher.UIThread.Invoke(() =>
        {
            scores.Clear();
            foreach (var score in orderedScores)
            {
                scores.Add(score);
            }
        });
    }
}
