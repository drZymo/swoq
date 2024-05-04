using Swoq.Interface;

namespace Swoq.Server;

public class GameServicePostman
{
    public void RaiseStarted(string playerName, Guid gameId, StartRequest request, StartResponse response)
    {
        Started?.Invoke(this, (playerName, gameId, request, response));
    }
    public event EventHandler<(string playerName, Guid gameId, StartRequest request, StartResponse response)>? Started;

    public void RaiseActed(Guid gameId, ActionRequest request, ActionResponse response)
    {
        Acted?.Invoke(this, (gameId, request, response));
    }
    public event EventHandler<(Guid gameId, ActionRequest request, ActionResponse response)>? Acted;
}
