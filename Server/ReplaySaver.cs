using Google.Protobuf;
using Microsoft.Extensions.Options;
using Swoq.Server.Data;
using Swoq.Server.Services;
using System.Collections.Concurrent;
using System.Globalization;

namespace Swoq.Server;

using Message = (Guid gameId, IMessage message);

internal class ReplaySaver : IDisposable
{
    private readonly GameServicePostman gameServicePostman;
    private readonly ILogger<ReplaySaver> logger;
    private readonly string replaysFolder;

    public ReplaySaver(GameServicePostman gameServicePostman, ILogger<ReplaySaver> logger, IOptions<ReplayStorageSettings> replayStorageSettings)
    {
        this.gameServicePostman = gameServicePostman;
        this.logger = logger;
        this.replaysFolder = Path.Combine(AppContext.BaseDirectory, replayStorageSettings.Value.Folder);

        handleMessagesThread = new Thread(new ThreadStart(HandleMessages));
        handleMessagesThread.Start();

        this.gameServicePostman.Started += OnGameStarted;
        this.gameServicePostman.Acted += OnGameActed;

        logger.LogInformation("Replays are saved to {Folder}", replaysFolder);
    }

    public void Dispose()
    {
        gameServicePostman.Acted -= OnGameActed;
        gameServicePostman.Started -= OnGameStarted;

        cancel.Cancel();
        handleMessagesThread.Join();

        if (previousStream != null)
        {
            previousStream.Dispose();
            previousStream = null;
        }
    }

    private readonly CancellationTokenSource cancel = new();
    private readonly Thread handleMessagesThread;

    private readonly ConcurrentDictionary<Guid, string> filePaths = new();

    private readonly SemaphoreSlim messagesSemaphore = new(0);
    private readonly ConcurrentQueue<Message> messages = new();

    private void OnGameStarted(object? sender, StartedEventArgs e)
    {
        // Only save replays of quests
        if (e.Request.HasLevel) return;

        // Register filename for this game id
        var dateTimeStr = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string filename = Path.Combine(replaysFolder, $"{e.Request.UserName} - {dateTimeStr} - {e.GameId}.swoq");
        if (!filePaths.TryAdd(e.GameId, filename)) return;

        // Store start
        Enqueue(e.GameId, e.Request);
        Enqueue(e.GameId, e.Response);
    }

    private void OnGameActed(object? sender, ActedEventArgs e)
    {
        // Only save messages of registered games
        if (!filePaths.ContainsKey(e.GameId)) return;

        // Store it
        Enqueue(e.GameId, e.Request);
        Enqueue(e.GameId, e.Response);
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

    private string? previousFilePath = null;
    private FileStream? previousStream = null;

    private void WriteMessage(Guid gameId, IMessage message)
    {
        try
        {
            // Is this a registered game
            if (!filePaths.TryGetValue(gameId, out var filePath)) return;

            // Create dir if needed
            var dir = Path.GetDirectoryName(filePath);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }


            // Append serialized message to the end
            FileStream stream;
            if (previousFilePath != null && previousStream != null)
            {
                if (previousFilePath != filePath)
                {
                    previousFilePath = null;
                    previousStream.Dispose();
                    previousStream = null;

                    stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
                }
                else
                {
                    stream = previousStream;
                }
            }
            else
            {
                stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            }
            stream.Seek(0, SeekOrigin.End);
            message.WriteDelimitedTo(stream);

            previousFilePath = filePath;
            previousStream = stream;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WriteMessage failed");
        }
    }
}
