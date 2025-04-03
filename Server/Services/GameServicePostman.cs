using Swoq.Interface;

namespace Swoq.Server.Services;

public class StartedEventArgs(string userName, Guid gameId, StartRequest request, StartResponse response) : EventArgs
{
    public string UserName { get; } = userName;
    public Guid GameId { get; } = gameId;
    public StartRequest Request { get; } = request;
    public StartResponse Response { get; } = response;
}

public class ActedEventArgs(Guid gameId, ActRequest request, ActResponse response) : EventArgs
{
    public Guid GameId { get; } = gameId;
    public ActRequest Request { get; } = request;
    public ActResponse Response { get; } = response;
}

public class GameServicePostman
{
    public void RaiseStarted(string userName, Guid gameId, StartRequest request, StartResponse response)
    {
        Started?.Invoke(this, new(userName, gameId, request, response));
    }
    public event EventHandler<StartedEventArgs>? Started;

    public void RaiseActed(Guid gameId, ActRequest request, ActResponse response)
    {
        Acted?.Invoke(this, new(gameId, request, response));
    }
    public event EventHandler<ActedEventArgs>? Acted;
}
