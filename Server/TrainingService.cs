using Grpc.Core;
using Swoq.Interface;

namespace Swoq.Server;

internal class TrainingService(ILogger<TrainingService> logger, TrainingServer server, ITrainingObserver observer) : Training.TrainingBase
{
    public override Task<StartResponse> Start(StartTrainingRequest request, ServerCallContext context)
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
                response.State = startResult.State.Convert();

                // Report
                observer.Started(startResult.PlayerName, startResult.GameId, request, response);
            }
            catch (Exception ex)
            {
                var (result, state) = ServiceUtil.ResultFromException(ex, logger);
                response.Result = result;
                response.State = state?.Convert();
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
                gameId = Guid.Parse(request.GameId);

                DirectedAction? action1 = (request.HasAction1 && request.HasDirection1)
                    ? new DirectedAction(request.Action1.Convert(), request.Direction1.Convert())
                    : null;

                DirectedAction? action2 = (request.HasAction2 && request.HasDirection2)
                    ? new DirectedAction(request.Action2.Convert(), request.Direction2.Convert())
                    : null;

                var state = server.Act(gameId.Value, action1, action2);

                response.Result = Result.Ok;
                response.State = state.Convert();
            }
            catch (Exception ex)
            {
                var (result, state) = ServiceUtil.ResultFromException(ex, logger);
                response.Result = result;
                response.State = state?.Convert();
            }

            // Report
            if (gameId.HasValue)
            {
                observer.Acted(gameId.Value, request, response);
            }

            return response;
        });
    }
}