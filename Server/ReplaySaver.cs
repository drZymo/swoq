using Google.Protobuf;
using Swoq.Interface;
using System.Collections.Concurrent;
using System.Collections.Immutable;
namespace Swoq.Server;

using Message = (Guid gameId, IMessage message);

internal class ReplaySaver : ITrainingObserver, IDisposable
{
    private static readonly string TrainingFolder = @"D:\Projects\swoq-stuff\Replays";

    private readonly ILogger<ReplaySaver> logger;

    public ReplaySaver(ILogger<ReplaySaver> logger)
    {
        this.logger = logger;

        handleMessagesThread = new Thread(new ThreadStart(HandleMessages));
        handleMessagesThread.Start();
    }

    public void Dispose()
    {
        cancel.Cancel();
        handleMessagesThread.Join();
    }

    private readonly CancellationTokenSource cancel = new();
    private readonly Thread handleMessagesThread;

    private readonly object playerNamesWriteMutex = new();
    private IImmutableDictionary<Guid, string> playerNames = ImmutableDictionary<Guid, string>.Empty;

    private readonly SemaphoreSlim messagesSemaphore = new(0);
    private readonly ConcurrentQueue<Message> messages = new();

    void ITrainingObserver.Started(string playerName, Guid gameId, StartTrainingRequest request, StartResponse response)
    {
        // Register new player name
        lock (playerNamesWriteMutex)
        {
            playerNames = playerNames.Add(gameId, playerName);
        }

        Enqueue(gameId, request);
        Enqueue(gameId, response);
    }

    void ITrainingObserver.Acted(Guid gameId, ActionRequest request, ActionResponse response)
    {
        Enqueue(gameId, request);
        Enqueue(gameId, response);
    }

    private void Enqueue(Guid gameId, IMessage message)
    {
        messages.Enqueue((gameId, message));
        messagesSemaphore.Release();
    }

    private void HandleMessages()
    {
        try
        {
            while (!cancel.IsCancellationRequested)
            {
                messagesSemaphore.Wait(cancel.Token);
                messages.TryDequeue(out var msg);
                WriteMessage(msg.gameId, msg.message);
            }
        }
        catch (OperationCanceledException)
        {
            // Exit gracefully
            return;
        }
    }

    private void WriteMessage(Guid gameId, IMessage message)
    {
        try
        {
            // Get player name (must be registered before)
            var playerName = playerNames[gameId];

            // Open file (create dir if needed)
            var path = Path.Combine(TrainingFolder, playerName, $"{gameId}.bin");
            var dir = Path.GetDirectoryName(path);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }
            using var file = File.OpenWrite(path);

            // Append
            file.Seek(0, SeekOrigin.End);

            // Serialize messages
            message.WriteDelimitedTo(file);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WriteMessage failed");
        }
    }
}
