using Grpc.Core;
using Swoq.Interface;

namespace Swoq.Server;

public class TrainingService(TrainingServer server) : Training.TrainingBase
{
    public override Task<StartResponse> StartGame(StartRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            var response = new StartResponse();

            try
            {
                var (gameId, height, width, state) = server.StartGame();

                response.Result = Result.Ok;
                response.GameId = gameId.ToString();
                response.Height = height;
                response.Width = width;
                response.State = CreateState(state);
            }
            catch
            {
                response.Result = Result.InternalError;
            }

            return response;
        });
    }

    public override Task<ActionResponse> Move(ActionRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            var response = new ActionResponse();
            try
            {
                var gameId = Guid.Parse(request.GameId);
                var (success, state) = server.Move(gameId, Convert(request.Direction));

                response.Result = success ? Result.Ok : Result.MoveNotAllowed;
                response.State = CreateState(state);
            }
            catch
            {
                response.Result = Result.InternalError;
            }
            return response;
        });
    }

    public override Task<ActionResponse> Use(ActionRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            var response = new ActionResponse();
            try
            {
                var gameId = Guid.Parse(request.GameId);
                var (success, state) = server.Use(gameId, Convert(request.Direction));

                response.Result = success ? Result.Ok : Result.UseNotAllowed;
                response.State = CreateState(state);
            }
            catch
            {
                response.Result = Result.InternalError;
            }
            return response;
        });
    }


    private static State CreateState(GameState gameState)
    {
        var state = new State();
        state.Map.AddRange(gameState.Map);
        state.Finished = gameState.Finished;
        state.Inventory = gameState.Inventory;
        return state;
    }

    private static Direction Convert(Interface.Direction direction) => direction switch
    {
        Interface.Direction.North => Direction.North,
        Interface.Direction.East => Direction.East,
        Interface.Direction.South => Direction.South,
        Interface.Direction.West => Direction.West,
        _ => throw new NotImplementedException(),
    };
}
