using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Swoq.Interface;
using System.Collections.Concurrent;

namespace Swoq.Server.Services;

public class QuestMonitorService : Interface.QuestMonitorService.QuestMonitorServiceBase, IDisposable
{
    private readonly GameServicePostman gameServicePostman;

    private readonly ConcurrentQueue<QuestUpdate> updates = new();
    private readonly SemaphoreSlim updatesCount = new(0);

    public QuestMonitorService(GameServicePostman gameServicePostman)
    {
        this.gameServicePostman = gameServicePostman;

        this.gameServicePostman.Started += OnStarted;
        this.gameServicePostman.Acted += OnActed;
    }

    public void Dispose()
    {
        gameServicePostman.Acted -= OnActed;
        gameServicePostman.Started -= OnStarted;
    }

    public override async Task Monitor(Empty request, IServerStreamWriter<QuestUpdate> responseStream, ServerCallContext context)
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

    private void OnStarted(object? sender, (string playerName, Guid gameId, StartRequest request, StartResponse response) e)
    {
        var isQuest = !e.request.HasLevel;
        if (isQuest)
        {
            currentQuestGameId = e.gameId;

            var update = new QuestUpdate
            {
                GameId = e.gameId.ToString(),
                Player = e.playerName,
                Started = new QuestStarted
                {
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
            var update = new QuestUpdate
            {
                GameId = e.gameId.ToString(),
                Acted = new QuestActed
                {
                    Request = e.request,
                    Response = e.response
                },
            };

            updates.Enqueue(update);
            updatesCount.Release();
        }
    }
}
