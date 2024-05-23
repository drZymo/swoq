using Avalonia.Threading;
using Grpc.Net.Client;
using Swoq.InfraUI.ViewModels;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Swoq.QuestDashboard.ViewModels;

internal class ScoresViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(3);

    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Thread pollThread;

    public ScoresViewModel()
    {
        Scores = new ReadOnlyObservableCollection<Score>(scores);

        pollThread = new Thread(new ThreadStart(PollThread));
        pollThread.Start();
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        pollThread.Join();
    }

    public record Score(string PlayerName, int Level, int LengthTicks, int LengthSeconds);


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

    private async void PollThread()
    {
        var callOptions = new Grpc.Core.CallOptions(cancellationToken: cancellationTokenSource.Token);

        while (!cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                bool connected = false;
                Dispatcher.UIThread.Invoke(() => { StatusMessage = "Connecting..."; });

                using var channel = GrpcChannel.ForAddress("http://localhost:5009");
                var client = new Interface.PlayerService.PlayerServiceClient(channel);

                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    var orderedScores = client.GetScores(new Google.Protobuf.WellKnownTypes.Empty(), callOptions).
                        Scores_.
                        Select(s => new Score(s.PlayerName, s.Level, s.LengthTicks, s.LengthSeconds)).
                        OrderByDescending(s => s.Level).
                        ThenBy(s => s.LengthTicks).
                        ThenBy(s => s.LengthSeconds).
                        ToImmutableArray();

                    if (!connected)
                    {
                        Dispatcher.UIThread.Invoke(() => { StatusMessage = "Connected"; });
                        connected = true;
                    }

                    Dispatcher.UIThread.Invoke(() =>
                    {
                        scores.Clear();
                        foreach (var score in orderedScores)
                        {
                            scores.Add(score);
                        }
                    });

                    await Task.Delay(PollDelay, cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Stop gracefully on cancel
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