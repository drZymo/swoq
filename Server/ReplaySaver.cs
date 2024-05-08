using Google.Protobuf;
using Microsoft.Extensions.Options;
using Swoq.Interface;
using Swoq.Server.Data;
using Swoq.Server.Services;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Swoq.Server;

using Message = (Guid gameId, IMessage message);

internal class ReplaySaver : IDisposable
{
    private readonly GameServicePostman gameServicePostman;
    private readonly ILogger<ReplaySaver> logger;
    private readonly ReplayStorageSettings replayStorageSettings;

    public ReplaySaver(GameServicePostman gameServicePostman, ILogger<ReplaySaver> logger, IOptions<ReplayStorageSettings> replayStorageSettings)
    {
        this.gameServicePostman = gameServicePostman;
        this.logger = logger;
        this.replayStorageSettings = replayStorageSettings.Value;

        handleMessagesThread = new Thread(new ThreadStart(HandleMessages));
        handleMessagesThread.Start();

        this.gameServicePostman.Started += OnGameStarted;
        this.gameServicePostman.Acted += OnGameActed;
    }

    public void Dispose()
    {
        gameServicePostman.Acted -= OnGameActed;
        gameServicePostman.Started -= OnGameStarted;

        cancel.Cancel();
        handleMessagesThread.Join();
    }

    private readonly CancellationTokenSource cancel = new();
    private readonly Thread handleMessagesThread;

    private readonly object filenamesWriteMutex = new();
    private IImmutableDictionary<Guid, string> filenames = ImmutableDictionary<Guid, string>.Empty;

    private readonly SemaphoreSlim messagesSemaphore = new(0);
    private readonly ConcurrentQueue<Message> messages = new();

    private void OnGameStarted(object? sender, (string playerName, Guid gameId, StartRequest request, StartResponse response) e)
    {
        // Register filename for this game id
        string filename;
        if (e.request.HasLevel)
        {
            filename = Path.Combine(replayStorageSettings.TrainingFolder, e.playerName, $"level {e.request.Level} - {e.gameId}.bin");
        }
        else
        {
            filename = Path.Combine(replayStorageSettings.QuestFolder, $"{e.playerName} - {e.gameId}.bin");
        }

        lock (filenamesWriteMutex)
        {
            filenames = filenames.Add(e.gameId, filename);
        }

        logger.LogInformation("New replay started at {path}", filename);

        Enqueue(e.gameId, e.request);
        Enqueue(e.gameId, e.response);
    }

    private void OnGameActed(object? sender, (Guid gameId, ActionRequest request, ActionResponse response) e)
    {
        Enqueue(e.gameId, e.request);
        Enqueue(e.gameId, e.response);
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
            // Get file name (must be registered before)
            var filename = filenames[gameId];

            // Create dir if needed
            var dir = Path.GetDirectoryName(filename);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }

            // Append serialized message to the end
            using var file = File.OpenWrite(filename);
            file.Seek(0, SeekOrigin.End);
            message.WriteDelimitedTo(file);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WriteMessage failed");
        }
    }
}
