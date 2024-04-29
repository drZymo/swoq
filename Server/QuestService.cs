using Grpc.Core;
using Swoq.Interface;

namespace Swoq.Server;

internal class QuestService(ILogger<QuestService> logger, QuestServer server) : Interface.Quest.QuestBase
{
    public override Task<StartResponse> Start(StartQuestRequest request, ServerCallContext context)
    {
        return Task.Run(() =>
        {
            var response = new StartResponse();
            try
            {
                var startResult = server.Start(request.PlayerId);

                response.Result = Result.Ok;
                response.GameId = startResult.GameId.ToString();
                response.Height = startResult.Height;
                response.Width = startResult.Width;
                response.VisibilityRange = startResult.VisibilityRange;
                response.State = startResult.State.Convert();
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
            var response = new ActionResponse();
            try
            {
                var questId = Guid.Parse(request.GameId);

                DirectedAction? action1 = null;
                if (request.HasAction1 && request.HasDirection1)
                {
                    action1 = new DirectedAction(request.Action1.Convert(), request.Direction1.Convert());
                }

                DirectedAction? action2 = null;
                if (request.HasAction2 && request.HasDirection2)
                {
                    action2 = new DirectedAction(request.Action2.Convert(), request.Direction2.Convert());
                }

                var state = server.Act(questId, action1, action2);

                response.Result = Result.Ok;
                response.State = state.Convert();
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
}