using Avalonia.Threading;
using Grpc.Net.Client;
using Swoq.InfraUI.ViewModels;
using Swoq.Interface;
using Swoq.ReplayViewer.ViewModels;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Swoq.QuestDashboard.ViewModels;

internal class GameStateMonitorViewModel : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Thread monitorThread;

    public GameStateMonitorViewModel()
    {
        QueueUsers = new(queuedUsers);
        TrainingSessions = new(trainingSessions);

        monitorThread = new Thread(new ThreadStart(MonitorThread));
        monitorThread.Start();
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        monitorThread.Join();
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

    private readonly ObservableCollection<TrainingSessionViewModel> trainingSessions = [];
    public ReadOnlyObservableCollection<TrainingSessionViewModel> TrainingSessions { get; }

    private async void MonitorThread()
    {
        var callOptions = new Grpc.Core.CallOptions(cancellationToken: cancellationTokenSource.Token);
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                bool connected = false;
                Dispatcher.UIThread.Invoke(() => { StatusMessage = "Connecting..."; });
                using var channel = GrpcChannel.ForAddress("http://localhost:5009");
                var client = new Interface.MonitorService.MonitorServiceClient(channel);

                GameStateBuilder? gameStateBuilder = null;

                var call = client.Monitor(new Google.Protobuf.WellKnownTypes.Empty(), callOptions);
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
                        UpdateTrainingSessions(message);
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

    private void UpdateTrainingSessions(Update message)
    {
        var newTrainingSessions = message.TrainingUpdate.Sessions;

        var newIds = newTrainingSessions.Select(s => s.GameId).ToImmutableHashSet();
        var oldIds = trainingSessions.Select(s => s.Id).ToImmutableHashSet();

        var addedSessionIds = newIds.Except(oldIds);
        var removedSessionIds = oldIds.Except(newIds);
        var updatedSessionIds = newIds.Intersect(newIds);

        // Remove sessions
        var sessionsToRemove = trainingSessions.Where(s => removedSessionIds.Contains(s.Id)).ToImmutableArray();
        foreach (var session in sessionsToRemove)
        {
            trainingSessions.Remove(session);
        }

        // Add sessions at the start
        var sessionsToAdd = newTrainingSessions.Where(s => addedSessionIds.Contains(s.GameId)).ToImmutableArray();
        foreach (var session in sessionsToAdd)
        {
            trainingSessions.Insert(
                0,
                new TrainingSessionViewModel(
                    session.GameId,
                    session.UserName,
                    session.Level,
                    session.IsActive,
                    session.IsFinished));
        }

        // Update existing
        foreach (var id in updatedSessionIds)
        {
            var newSession = newTrainingSessions.Where(s => s.GameId == id).FirstOrDefault();
            var oldSession = trainingSessions.Where(s => s.Id == id).FirstOrDefault();

            if (newSession != null && oldSession != null)
            {
                oldSession.IsActive = newSession.IsActive;
                oldSession.IsFinished = newSession.IsFinished;
            }
        }
    }
}
