using Grpc.Core;
using Swoq.Interface;

namespace Swoq.Server.Services;

internal class GameService(ILogger<GameService> logger, GameServer server, GameServicePostman gameServicePostman) : Interface.GameService.GameServiceBase
{
    public override Task<StartResponse> Start(StartRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            var response = new StartResponse();
            try
            {
                var startResult = server.Start(request.UserId, request.HasLevel ? request.Level : null);

                response.Result = Result.Ok;
                response.GameId = startResult.GameId.ToString();
                response.Height = Parameters.MapHeight;
                response.Width = Parameters.MapWidth;
                response.VisibilityRange = Parameters.PlayerVisibilityRange;
                response.State = startResult.State.Convert();

                // Report
                gameServicePostman.RaiseStarted(startResult.UserName, startResult.GameId, request, response);
            }
            catch (SwoqGameException ex)
            {
                response.Result = ServiceUtil.ResultFromException(ex, logger);
                response.State = ex.State.Convert();
            }
            catch (SwoqException ex)
            {
                response.Result = ServiceUtil.ResultFromException(ex, logger);
                response.State = null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Internal error");
                response.Result = Result.InternalError;
                response.State = null;
            }
            return response;
        });
    }

    public override Task<ActionResponse> Act(ActionRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            Guid? gameId = null;
            var response = new ActionResponse();

            try
            {
                if (!Guid.TryParse(request.GameId, out var id))
                {
                    throw new UnknownGameIdException();
                }
                gameId = id;

                DirectedAction? action1 = request.HasAction ? request.Action : null;
                DirectedAction? action2 = request.HasAction2 ? request.Action2 : null;

                var state = server.Act(gameId.Value, action1, action2);

                response.Result = Result.Ok;
                response.State = state.Convert();
            }
            catch (SwoqGameException ex)
            {
                response.Result = ServiceUtil.ResultFromException(ex, logger);
                response.State = ex.State.Convert();
            }
            catch (SwoqException ex)
            {
                response.Result = ServiceUtil.ResultFromException(ex, logger);
                response.State = null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Internal error");
                response.Result = Result.InternalError;
                response.State = null;
            }

            // Report
            if (gameId.HasValue)
            {
                gameServicePostman.RaiseActed(gameId.Value, request, response);
            }

            return response;
        });
    }
}