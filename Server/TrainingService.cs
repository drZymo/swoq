using Grpc.Core;
using Swoq.Interface;

namespace Swoq.Server;

public class TrainingService : Training.TrainingBase
{
    private readonly TrainingServer server;

    public TrainingService(TrainingServer server)
    {
        this.server = server;
    }

    public override Task<StartResponse> StartGame(StartRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            var gameId = server.StartGame();

            var state = server.GetGameState(gameId);
            var response = new StartResponse();
            response.Result = Result.Ok;
            response.GameId = gameId.ToString();
            response.State.AddRange(state);
            return response;
        });
    }
}
