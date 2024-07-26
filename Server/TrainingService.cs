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
            var response = new StartResponse();

            try
            {
                var (gameId, height, width, map) = server.StartGame();

                response.Result = Result.Ok;
                response.GameId = gameId.ToString();
                response.Height = height;
                response.Width = width;
                response.State = CreateState(map, false);
            }
            catch
            {
                response.Result = Result.InternalError;
            }

            return response;
        });
    }

    public override Task<MoveResponse> Move(MoveRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            var response = new MoveResponse();
            try
            {
                var gameId = Guid.Parse(request.GameId);
                var (success, map, finished) = server.Move(gameId, Convert(request.Direction));

                response.Result = success ? Result.Ok : Result.MoveNotAllowed;
                response.State = CreateState(map, finished);
            }
            catch
            {
                response.Result = Result.InternalError;
            }
            return response;
        });
    }


    private static State CreateState(int[] map, bool finished)
    {
        var state = new Interface.State();
        state.Map.AddRange(map);
        state.Finished = finished;
        return state;
    }

    private static Direction Convert(Interface.Direction direction)
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
