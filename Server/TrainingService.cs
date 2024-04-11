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
            var (gameId, state) = server.StartGame();

            // TODO: Exception handling
            var response = new StartResponse();
            response.Result = Result.Ok;
            response.GameId = gameId.ToString();
            response.State.AddRange(state);
            return response;
        });
    }

    public override Task<MoveResponse> Move(MoveRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            var gameId = Guid.Parse(request.GameId);
            var state = server.Move(gameId, Convert(request.Direction));
            
            // TODO: Move result
            // TODO: Exception handling
            var response = new MoveResponse();
            response.Result = Result.Ok;
            response.State.AddRange(state);
            return response;
        });
    }

    private Swoq.Server.Direction Convert(Swoq.Interface.Direction direction)
    {
        switch (direction)
        {
            case Interface.Direction.North: return Direction.North;
            case Interface.Direction.East: return Direction.East;
            case Interface.Direction.South: return Direction.South;
            case Interface.Direction.West: return Direction.West;
        }
        return Direction.North;
    }
}
