using Grpc.Core;
using Swoq.Interface;

namespace Swoq.Server.Services;

internal class GameService(ILogger<GameService> logger, IGameServer server, GameServicePostman gameServicePostman) : Interface.GameService.GameServiceBase
{
    public override Task<StartResponse> Start(StartRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            var response = new StartResponse();
            try
            {
                int? level = request.HasLevel ? request.Level : null;
                // Seed may only be given for Training games. Quest games are always random.
                int? seed = (request.HasLevel && request.HasSeed) ? request.Seed : null;
                var startResult = server.Start(request.UserId, request.UserName, level, seed);

                response.Result = StartResult.Ok;
                response.GameId = startResult.GameId.ToString();
                response.MapHeight = Parameters.MapHeight;
                response.MapWidth = Parameters.MapWidth;
                response.VisibilityRange = Parameters.PlayerVisibilityRange;
                response.State = startResult.State.Convert();
                response.Seed = startResult.Seed;

                // Report
                gameServicePostman.RaiseStarted(startResult.GameId, request, response);
            }
            catch (GameServerStartException ex)
            {
                if (ex.Result == StartResult.InternalError) logger.LogError(ex, "Internal error");
                response.Result = ex.Result;
                response.State = null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Internal error");
                response.Result = StartResult.InternalError;
                response.State = null;
            }
            return response;
        });
    }

    public override Task<ActResponse> Act(ActRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            Guid? gameId = null;
            var response = new ActResponse();

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

                response.Result = ActResult.Ok;
                response.State = state.Convert();
            }
            catch (GameServerActException ex)
            {
                if (ex.Result == ActResult.InternalError) logger.LogError(ex, "Internal error");
                response.Result = ex.Result;
                response.State = ex.State?.Convert();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Internal error");
                response.Result = ActResult.InternalError;
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
