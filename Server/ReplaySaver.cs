using Google.Protobuf;
using Microsoft.Extensions.Options;
using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server.Data;
using Swoq.Server.Services;
using System.Collections.Concurrent;

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

    private readonly ConcurrentDictionary<Guid, string> filenames = new();

    private readonly SemaphoreSlim messagesSemaphore = new(0);
    private readonly ConcurrentQueue<Message> messages = new();

    private void OnGameStarted(object? sender, (string userName, Guid gameId, StartRequest request, StartResponse response) e)
    {
        // Only save replays of quests
        if (e.request.HasLevel) return;

        // Register filename for this game id
        string sanitizedUserName = Uri.EscapeDataString(e.userName);
        string filename = Path.Combine(AppContext.BaseDirectory, replayStorageSettings.Folder, $"{sanitizedUserName} - {e.gameId}.bin");
        filenames.TryAdd(e.gameId, filename);

        // Store header
        var header = new ReplayHeader { UserName = e.userName, DateTime = Clock.Now.ToString("s") };
        Enqueue(e.gameId, header);

        // Store start
        Enqueue(e.gameId, e.request);
        Enqueue(e.gameId, e.response);
    }

    private void OnGameActed(object? sender, (Guid gameId, ActionRequest request, ActionResponse response) e)
    {
        // Only save messages of registered games
        if (!filenames.ContainsKey(e.gameId)) return;

        // Store it
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
                if (messages.TryDequeue(out var msg))
                {
                    WriteMessage(msg.gameId, msg.message);
                }
            }
        }
        // Exit gracefully on cancellation
        catch (OperationCanceledException) { return; }
    }

    private void WriteMessage(Guid gameId, IMessage message)
    {
        try
        {
            // Is this a registered game
            if (!filenames.TryGetValue(gameId, out var filename)) return;

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
