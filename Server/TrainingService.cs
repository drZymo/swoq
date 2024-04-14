using Grpc.Core;
using Swoq.Interface;

namespace Swoq.Server;

internal class TrainingService(ILogger<TrainingService> logger, TrainingServer server) : Training.TrainingBase
{
    public override Task<StartResponse> StartGame(StartRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            var response = new StartResponse();
            try
            {
                var startResult = server.StartGame(request.PlayerId, request.Level);

                response.Result = Result.Ok;
                response.GameId = startResult.GameId.ToString();
                response.Height = startResult.Height;
                response.Width = startResult.Width;
                response.VisibilityRange = startResult.VisibilityRange;
                response.State = CreateState(startResult.State);
            }
            catch (Exception ex)
            {
                response.Result = ResultFromException(ex);
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
            catch (Exception ex)
            {
                response.Result = ResultFromException(ex);
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
            catch (Exception ex)
            {
                response.Result = ResultFromException(ex);
            }
            return response;
        });
    }


    private static State CreateState(GameState gameState)
    {
        var state = new State();
        state.PlayerPos = new Position() { X = gameState.playerX, Y = gameState.playerY };
        state.Surroundings.AddRange(gameState.Surroundings);
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

    private Result ResultFromException(Exception ex)
    {
        switch (ex)
        {
            case PlayerUnknownException: return Result.PlayerUnknown;
            case LevelNotAvailableException: return Result.LevelNotAvailable;
        }
        logger.LogError(ex, "Internal error");
        return Result.InternalError;
    }
}