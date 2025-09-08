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

    private record QuestData(GameObservationBuilder Builder, GameObservationViewModel ViewModel);
    private ImmutableDictionary<string, QuestData> quests = ImmutableDictionary<string, QuestData>.Empty;

    public GameObserverViewModel()
    {
        ActiveQuests = new(activeQuests);
        InactiveQuests = new(inactiveQuests);

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

    private readonly ObservableCollection<GameObservationViewModel> activeQuests = [];
    public ReadOnlyObservableCollection<GameObservationViewModel> ActiveQuests { get; private set; }

    private readonly ObservableCollection<GameObservationViewModel> inactiveQuests = [];
    public ReadOnlyObservableCollection<GameObservationViewModel> InactiveQuests { get; private set; }

    private readonly ObservableCollection<string> queuedUsers = [];
    public ReadOnlyObservableCollection<string> QueueUsers { get; }


    private readonly ObservableCollection<SessionViewModel> sessions = [];
    public ReadOnlyObservableCollection<SessionViewModel> Sessions { get; }


    public record Score(string UserName, int Level, int LengthTicks, int LengthSeconds, bool QuestFinished)
    {
        public bool HasProgress => Level > 0;
    }

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

    public int columns = 1;
    public int Columns
    {
        get => columns;
        private set
        {
            if (value != columns)
            {
                columns = value;
                OnPropertyChanged();
            }
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
                using var channel = GrpcChannel.ForAddress("http://localhost:5001");
                var client = new DashboardService.DashboardServiceClient(channel);

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
                        HandleQuestStarted(message.QuestStarted);
                    }

                    if (message.QuestActed != null)
                    {
                        HandleQuestActed(message.QuestActed);
                    }

                    if (message.QueueUpdate != null)
                    {
                        HandleQueueUpdate(message.QueueUpdate);
                    }

                    if (message.SessionsUpdate != null)
                    {
                        HandleSessionsUpdate(message.SessionsUpdate);
                    }

                    if (message.ScoresUpdate != null)
                    {
                        HandleScoresUpdate(message.ScoresUpdate);
                    }

                    if (message.StatisticsUpdate != null)
                    {
                        HandleStatistics(message.StatisticsUpdate);
                    }

                    if (message.QuestStatusChanged != null)
                    {
                        HandleQuestStatusChanged(message.QuestStatusChanged);
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

    private void HandleQuestStarted(QuestStarted update)
    {
        var builder = new GameObservationBuilder(update.GameId, update.Response.MapHeight, update.Response.MapWidth, update.Response.VisibilityRange, update.Request.UserName);
        var gameState = builder.BuildNext(null, update.Response.State, update.Response.Result, Dispatcher.UIThread);

        GameObservationViewModel viewModel = Dispatcher.UIThread.Invoke(() =>
        {
            var vm = new GameObservationViewModel(gameState);
            activeQuests.Add(vm);
            UpdateColumns();
            return vm;
        });

        quests = quests.Add(update.GameId, new QuestData(builder, viewModel));
    }

    private void HandleQuestActed(QuestActed update)
    {
        if (quests.TryGetValue(update.GameId, out var questData))
        {
            var gameState = questData.Builder.BuildNext(update.Request, update.Response.State, update.Response.Result, Dispatcher.UIThread);
            Dispatcher.UIThread.Invoke(() =>
            {
                questData.ViewModel.SetGameObservation(gameState);
            });
            MakeInactiveIfFinished(questData.ViewModel);
        }
    }

    private void HandleQueueUpdate(QueueUpdate update)
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

    private void HandleSessionsUpdate(SessionsUpdate update)
    {
        var newSessions = update.Sessions;

        // Find out which game ids have been added, removed and updated
        var newIds = newSessions.Select(s => s.GameId).ToImmutableHashSet();
        var oldIds = sessions.Select(s => s.Id).ToImmutableHashSet();

        var addedSessionIds = newIds.Except(oldIds);
        var removedSessionIds = oldIds.Except(newIds);
        var updatedSessionIds = newIds.Intersect(newIds);

        // Remove related quests
        var gameObservationsToRemove = ImmutableList<GameObservationViewModel>.Empty;
        foreach (var gameId in removedSessionIds)
        {
            if (quests.TryGetValue(gameId, out var questData))
            {
                quests = quests.Remove(gameId);
                gameObservationsToRemove = gameObservationsToRemove.Add(questData.ViewModel);
            }
        }

        // Find sessions to remove and add
        var sessionsToRemove = sessions.Where(s => removedSessionIds.Contains(s.Id)).ToImmutableArray();
        var sessionsToAdd = newSessions.Where(s => addedSessionIds.Contains(s.GameId)).ToImmutableArray();

        // Remove view models on the UI thread
        Dispatcher.UIThread.Invoke(() =>
        {
            foreach (var gameObservation in gameObservationsToRemove)
            {
                activeQuests.Remove(gameObservation);
                inactiveQuests.Remove(gameObservation);
                gameObservation.Dispose();
            }
            UpdateColumns();

            foreach (var session in sessionsToRemove)
            {
                sessions.Remove(session);
            }

            // Add sessions at the start/top of the list
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

    private void HandleScoresUpdate(ScoresUpdate update)
    {
        var orderedScores = update.Scores.
            Select(s => new Score(s.UserName, s.Level, s.LengthTicks, s.LengthSeconds, s.QuestFinished)).
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

    private void HandleStatistics(StatisticsUpdate update)
    {
        EventsPerSecond = update.EventsPerSecond;
    }

    private void HandleQuestStatusChanged(QuestStatusChanged update)
    {
        if (quests.TryGetValue(update.GameId, out var questData))
        {
            var gameState = questData.Builder.SetGameStatus(update.Status, Dispatcher.UIThread);
            Dispatcher.UIThread.Invoke(() =>
            {
                questData.ViewModel.SetGameObservation(gameState);
            });
            MakeInactiveIfFinished(questData.ViewModel);
        }
    }

    private void UpdateColumns()
    {
        var count = ActiveQuests.Count;
        Columns = (int)Math.Ceiling(Math.Sqrt(count));
    }

    private void MakeInactiveIfFinished(GameObservationViewModel gameObservation)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (gameObservation.IsFinished && activeQuests.Remove(gameObservation))
            {
                inactiveQuests.Insert(0, gameObservation);
            }
        });
    }
}
