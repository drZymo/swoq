using Avalonia.Threading;
using Grpc.Net.Client;
using Swoq.InfraUI.ViewModels;
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
        QueuedPlayers = new(queuedPlayers);

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

    private readonly ObservableCollection<string> queuedPlayers = [];
    public ReadOnlyObservableCollection<string> QueuedPlayers { get; }

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
                var client = new Interface.QuestMonitorService.QuestMonitorServiceClient(channel);

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

                    if (message.Started != null)
                    {
                        Dispatcher.UIThread.Invoke(() => { GameState.Reset(); });

                        var started = message.Started;
                        gameStateBuilder = new GameStateBuilder(started.Response.Height, started.Response.Width, started.Response.VisibilityRange, started.Player);
                        var gameState = gameStateBuilder.BuildNext(null, started.Response.State, started.Response.Result, Dispatcher.UIThread);
                        Dispatcher.UIThread.Invoke(() => { GameState.SetGameState(gameState); });
                    }

                    if (message.Acted != null && gameStateBuilder != null)
                    {
                        var acted = message.Acted;
                        var gameState = gameStateBuilder.BuildNext(acted.Request, acted.Response.State, acted.Response.Result, Dispatcher.UIThread);
                        Dispatcher.UIThread.Invoke(() => { GameState.SetGameState(gameState); });
                    }

                    if (message.QueueUpdate != null)
                    {
                        var queuedPlayers = message.QueueUpdate.QueuedPlayers.ToImmutableArray();
                        Dispatcher.UIThread.Invoke(() =>
                        {
                            this.queuedPlayers.Clear();
                            foreach (var qp in queuedPlayers)
                            {
                                this.queuedPlayers.Add(qp);
                            }
                        });
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
}
