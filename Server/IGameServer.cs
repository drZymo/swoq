using Swoq.Interface;
using System.Collections.Immutable;

namespace Swoq.Server;

public class GameRemovedEventArgs(Guid gameId) : EventArgs
{
    public Guid GameId { get; } = gameId;
}

public class QueueUpdatedEventArgs(IImmutableList<string> queuedUsers) : EventArgs
{
    public IImmutableList<string> QueuedUsers { get; } = queuedUsers;
}

public interface IGameServer
{
    event EventHandler<GameRemovedEventArgs>? GameRemoved;
    event EventHandler<QueueUpdatedEventArgs>? QueueUpdated;

    GameStartResult Start(string userId, int? level, int? seed = null);
    GameState Act(Guid gameId, DirectedAction? action1 = null, DirectedAction? action2 = null);
}
