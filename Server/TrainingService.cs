using Grpc.Core;
using Swoc2024Server;
using Swoq.Interface;
using System.Collections.Immutable;

namespace Swoq.Server;

public class TrainingService : Swoq.Interface.Training.TrainingBase
{
    private readonly TrainingServer server;

    public TrainingService(TrainingServer server)
    {
        this.server = server;
    }

    public override Task<StartResponse> StartGame(StartRequest request, ServerCallContext context)
    {
        server.StartGame();
        return base.StartGame(request, context);
    }
}
