using Swoq.Interface;
using System.Collections.Immutable;

namespace Swoq.Server;

public record GameRemovedEventArgs(Guid GameId);
public record QueueUpdatedEventArgs(IImmutableList<string> QueuedUsers);

public interface IGameServer
{
    event EventHandler<GameRemovedEventArgs>? GameRemoved;
    event EventHandler<QueueUpdatedEventArgs>? QueueUpdated;

    GameStartResult Start(string userId, int? level, int? seed = null);
    GameState Act(Guid gameId, DirectedAction? action1 = null, DirectedAction? action2 = null);
}
