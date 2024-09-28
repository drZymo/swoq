using Avalonia.Threading;
using Grpc.Net.Client;
using Swoq.InfraUI.ViewModels;
using Swoq.Interface;
using Swoq.ReplayViewer.ViewModels;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Swoq.Dashboard.ViewModels;

internal class GameStateMonitorViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(3);

    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Thread getUpdatesThread;

    public GameStateMonitorViewModel()
    {
        QueueUsers = new(queuedUsers);
        TrainingSessions = new(trainingSessions);
        Scores = new(scores);

        getUpdatesThread = new Thread(new ThreadStart(GetUpdatesThread));
        getUpdatesThread.Start();
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        getUpdatesThread.Join();
    }

    private GameStateViewModel gameState = new(null);
    public GameStateViewModel GameState
    {
        get => gameState;
        private set
        {
            gameState = value;
            OnPropertyChanged();
        }
    }

    private readonly ObservableCollection<string> queuedUsers = [];
    public ReadOnlyObservableCollection<string> QueueUsers { get; }

    private readonly ObservableCollection<TrainingSessionViewModel> trainingSessions = [];
    public ReadOnlyObservableCollection<TrainingSessionViewModel> TrainingSessions { get; }

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
                using var channel = GrpcChannel.ForAddress("http://localhost:5009");
                var client = new DashboardService.DashboardServiceClient(channel);

                GameStateBuilder? gameStateBuilder = null;

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
                        Dispatcher.UIThread.Invoke(() => { GameState.Reset(); });

                        var started = message.QuestStarted;
                        gameStateBuilder = new GameStateBuilder(started.Response.Height, started.Response.Width, started.Response.VisibilityRange, started.UserName);
                        var gameState = gameStateBuilder.BuildNext(null, started.Response.State, started.Response.Result, Dispatcher.UIThread);
                        Dispatcher.UIThread.Invoke(() => { GameState.SetGameState(gameState); });
                    }

                    if (message.QuestActed != null && gameStateBuilder != null)
                    {
                        var acted = message.QuestActed;
                        var gameState = gameStateBuilder.BuildNext(acted.Request, acted.Response.State, acted.Response.Result, Dispatcher.UIThread);
                        Dispatcher.UIThread.Invoke(() => { GameState.SetGameState(gameState); });
                    }

                    if (message.QueueUpdate != null)
                    {
                        var queuedUsers = message.QueueUpdate.QueuedUsers.ToImmutableArray();
                        Dispatcher.UIThread.Invoke(() =>
                        {
                            this.queuedUsers.Clear();
                            foreach (var qu in queuedUsers)
                            {
                                this.queuedUsers.Add(qu);
                            }
                        });
                    }

                    if (message.TrainingUpdate != null)
                    {
                        UpdateTrainingSessions(message.TrainingUpdate);
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

    private void UpdateTrainingSessions(TrainingUpdate update)
    {
        var newTrainingSessions = update.Sessions;

        // Update events per second
        EventsPerSecond = update.EventsPerSecond;

        // Find out which game ids have been added, removed and updated
        var newIds = newTrainingSessions.Select(s => s.GameId).ToImmutableHashSet();
        var oldIds = trainingSessions.Select(s => s.Id).ToImmutableHashSet();

        var addedSessionIds = newIds.Except(oldIds);
        var removedSessionIds = oldIds.Except(newIds);
        var updatedSessionIds = newIds.Intersect(newIds);

        // Remove sessions
        var sessionsToRemove = trainingSessions.Where(s => removedSessionIds.Contains(s.Id)).ToImmutableArray();
        foreach (var session in sessionsToRemove)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                trainingSessions.Remove(session);
            });
        }

        // Add sessions at the start
        var sessionsToAdd = newTrainingSessions.Where(s => addedSessionIds.Contains(s.GameId)).ToImmutableArray();
        foreach (var session in sessionsToAdd)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                trainingSessions.Insert(
                0,
                new TrainingSessionViewModel(
                    session.GameId,
                    session.UserName,
                    session.Level,
                    session.IsActive,
                    session.IsFinished));
            });
        }

        // Update existing
        foreach (var id in updatedSessionIds)
        {
            var newSession = newTrainingSessions.Where(s => s.GameId == id).FirstOrDefault();
            var oldSession = trainingSessions.Where(s => s.Id == id).FirstOrDefault();

            if (newSession != null && oldSession != null)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
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
