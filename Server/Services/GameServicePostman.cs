using Swoq.Interface;

namespace Swoq.Server.Services;

public class GameServicePostman
{
    public void RaiseStarted(string userName, Guid gameId, StartRequest request, StartResponse response)
    {
        Started?.Invoke(this, (userName, gameId, request, response));
    }
    public event EventHandler<(string userName, Guid gameId, StartRequest request, StartResponse response)>? Started;

    public void RaiseActed(Guid gameId, ActionRequest request, ActionResponse response)
    {
        Acted?.Invoke(this, (gameId, request, response));
    }
    public event EventHandler<(Guid gameId, ActionRequest request, ActionResponse response)>? Acted;
}
