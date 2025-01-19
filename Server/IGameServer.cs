using Swoq.Interface;
using System.Collections.Immutable;

namespace Swoq.Server;

public record GameAddedEventArgs(Guid GameId, string UserName, int Level, bool IsQuest);
public record GameRemovedEventArgs(Guid GameId);
public record GameUpdatedEventArgs(Guid GameId, int Level, bool IsFinished);
public record QueueUpdatedEventArgs(IImmutableList<string> QueuedUsers);
public record StatisticsUpdatedEventArgs(float EventsPerSecond);

public interface IGameServer
{
    event EventHandler<GameAddedEventArgs>? GameAdded;
    event EventHandler<GameRemovedEventArgs>? GameRemoved;
    event EventHandler<GameUpdatedEventArgs>? GameUpdated;
    event EventHandler<QueueUpdatedEventArgs>? QueueUpdated;
    event EventHandler<StatisticsUpdatedEventArgs>? StatisticsUpdated;

    GameStartResult Start(string userId, int? level);
    GameState Act(Guid gameId, DirectedAction? action1 = null, DirectedAction? action2 = null);
}
