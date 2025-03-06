using Swoq.Interface;

namespace Swoq.Server.Services;

public class StartedEventArgs(string userName, Guid gameId, StartRequest request, StartResponse response) : EventArgs
{
    public string UserName { get; } = userName;
    public Guid GameId { get; } = gameId;
    public StartRequest Request { get; } = request;
    public StartResponse Response { get; } = response;
}

public class ActedEventArgs(Guid gameId, ActionRequest request, ActionResponse response) : EventArgs
{
    public Guid GameId { get; } = gameId;
    public ActionRequest Request { get; } = request;
    public ActionResponse Response { get; } = response;
}

public class GameServicePostman
{
    public void RaiseStarted(string userName, Guid gameId, StartRequest request, StartResponse response)
    {
        Started?.Invoke(this, new(userName, gameId, request, response));
    }
    public event EventHandler<StartedEventArgs>? Started;

    public void RaiseActed(Guid gameId, ActionRequest request, ActionResponse response)
    {
        Acted?.Invoke(this, new(gameId, request, response));
    }
    public event EventHandler<ActedEventArgs>? Acted;
}
