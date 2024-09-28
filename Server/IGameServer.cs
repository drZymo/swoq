using Swoq.Interface;
using System.Collections.Immutable;

namespace Swoq.Server;

public interface IGameServer
{
    event EventHandler<(Guid gameId, bool finished)>? GameActed;
    event EventHandler<(Guid gameId, string username, int? level)>? GameAdded;
    event EventHandler<Guid>? GameRemoved;
    event EventHandler<IImmutableList<string>>? QueueUpdated;

    GameState Act(Guid gameId, DirectedAction? action1 = null, DirectedAction? action2 = null);
    GameStartResult Start(string userId, int? level);
}
