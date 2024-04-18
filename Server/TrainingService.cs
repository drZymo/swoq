using Grpc.Core;
using Swoq.Interface;

namespace Swoq.Server;

internal class TrainingService(ILogger<TrainingService> logger, TrainingServer server) : Training.TrainingBase
{
    public override Task<StartResponse> Start(StartRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            var response = new StartResponse();
            try
            {
                var startResult = server.Start(request.PlayerId, request.Level);

                response.Result = Result.Ok;
                response.GameId = startResult.GameId.ToString();
                response.Height = startResult.Height;
                response.Width = startResult.Width;
                response.VisibilityRange = startResult.VisibilityRange;
                response.State = Convert(startResult.State);
            }
            catch (Exception ex)
            {
                response.Result = ResultFromException(ex);
            }
            return response;
        });
    }

    public override Task<ActionResponse> Act(ActionRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            var response = new ActionResponse();
            try
            {
                var gameId = Guid.Parse(request.GameId);

                switch (request.Action)
                {
                    case Interface.Action.Move:
                        {
                            var (success, state) = server.Move(gameId, Convert(request.Direction));
                            response.Result = success ? Result.Ok : Result.MoveNotAllowed;
                            response.State = Convert(state);
                        }
                        break;

                    case Interface.Action.Use:
                        {
                            var (success, state) = server.Use(gameId, Convert(request.Direction));
                            response.Result = success ? Result.Ok : Result.UseNotAllowed;
                            response.State = Convert(state);
                        }
                        break;

                    default:
                        response.Result = Result.UnknownAction;
                        break;
                }

            }
            catch (Exception ex)
            {
                response.Result = ResultFromException(ex);
            }
            return response;
        });
    }

    private static State Convert(GameState gameState)
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
            case UnknownPlayerException: return Result.UnknownPlayer;
            case UnknownGameIdException: return Result.UnknownGameId;
            case LevelNotAvailableException: return Result.LevelNotAvailable;
            case UnknownDirectionException: return Result.UnknownDirection;
        }
        logger.LogError(ex, "Internal error");
        return Result.InternalError;
    }
}