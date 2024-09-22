using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Swoq.Interface;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Swoq.Server.Services;

internal class MonitorService : Interface.MonitorService.MonitorServiceBase, IDisposable
{
    private readonly GameServicePostman gameServicePostman;
    private readonly GameServer gameServer;

    private readonly ConcurrentQueue<Update> updates = new();
    private readonly SemaphoreSlim updatesCount = new(0);

    public MonitorService(GameServicePostman gameServicePostman, GameServer gameServer)
    {
        this.gameServicePostman = gameServicePostman;
        this.gameServer = gameServer;

        this.gameServicePostman.Started += OnStarted;
        this.gameServicePostman.Acted += OnActed;
        this.gameServer.QueueUpdated += OnQueueUpdated;
    }

    public void Dispose()
    {
        gameServer.QueueUpdated -= OnQueueUpdated;
        gameServicePostman.Acted -= OnActed;
        gameServicePostman.Started -= OnStarted;
    }

    public override async Task Monitor(Empty request, IServerStreamWriter<Update> responseStream, ServerCallContext context)
    {
        try
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await updatesCount.WaitAsync(context.CancellationToken);
                if (updates.TryDequeue(out var update))
                {
                    await responseStream.WriteAsync(update);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Gracefull shutdown
        }
    }

    private Guid currentQuestGameId = Guid.Empty;

    private void OnStarted(object? sender, (string userName, Guid gameId, StartRequest request, StartResponse response) e)
    {
        var isQuest = !e.request.HasLevel;
        if (isQuest)
        {
            currentQuestGameId = e.gameId;

            var update = new Update
            {
                Started = new QuestStarted
                {
                    GameId = e.gameId.ToString(),
                    UserName = e.userName,
                    Request = e.request,
                    Response = e.response
                },
            };

            updates.Enqueue(update);
            updatesCount.Release();
        }
    }

    private void OnActed(object? sender, (Guid gameId, ActionRequest request, ActionResponse response) e)
    {
        if (e.gameId == currentQuestGameId)
        {
            var update = new Update
            {
                Acted = new QuestActed
                {
                    GameId = e.gameId.ToString(),
                    Request = e.request,
                    Response = e.response
                },
            };

            updates.Enqueue(update);
            updatesCount.Release();
        }
    }

    private void OnQueueUpdated(object? sender, IImmutableList<string> queue)
    {
        var update = new Update
        {
            QueueUpdate = new()
        };
        update.QueueUpdate.QueuedUsers.AddRange(queue);

        updates.Enqueue(update);
        updatesCount.Release();
    }
}
