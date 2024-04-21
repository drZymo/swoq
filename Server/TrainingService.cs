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

                var action1 = new DirectedAction(Convert(request.Action1), Convert(request.Direction1));

                DirectedAction? action2 = null;
                if (request.HasAction2 && request.HasDirection2)
                {
                    action2 = new DirectedAction(Convert(request.Action2), Convert(request.Direction2));
                }

                var state = server.Act(gameId, action1, action2);

                response.Result = Result.Ok;
                response.State = Convert(state);
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
        var state = new State
        {
            Finished = gameState.Finished,
            Player1 = Convert(gameState.Player1),
            Player2 = gameState.Player2 != null ? Convert(gameState.Player2) : null,
        };
        return state;
    }

    private static Interface.PlayerState Convert(PlayerState playerState)
    {
        var state = new Interface.PlayerState
        {
            Position = new Position { X = playerState.Position.x, Y = playerState.Position.y },
            Health = playerState.Health,
            Inventory = playerState.Inventory,
            HasSword = playerState.HasSword,
        };
        state.Surroundings.AddRange(playerState.Surroundings);
        return state;
    }

    private static Action Convert(Interface.Action action) => action switch
    {
        Interface.Action.Move => Action.Move,
        Interface.Action.Use => Action.Use,
        _ => throw new NotImplementedException(),
    };

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
            case PlayerAlreadyRegisteredException: return Result.PlayerAlreadyRegistered;
            case UnknownPlayerException: return Result.UnknownPlayer;
            case UnknownGameIdException: return Result.UnknownGameId;
            case LevelNotAvailableException: return Result.LevelNotAvailable;
            case MoveNotAllowedException: return Result.MoveNotAllowed;
            case UseNotAllowedException: return Result.UseNotAllowed;
            case UnknownActionException: return Result.UnknownAction;
            case UnknownDirectionException: return Result.UnknownDirection;
            case GameFinishedException: return Result.GameFinished;
            case Player2NotPresentException: return Result.Player2NotPresent;
        }
        logger.LogError(ex, "Internal error");
        return Result.InternalError;
    }
}