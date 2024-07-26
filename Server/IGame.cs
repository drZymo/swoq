using Swoq.Interface;

namespace Swoq.Server;

internal interface IGame
{
    Guid Id { get; }
    GameState State { get; }
    DateTime LastActionTime { get; }
    bool IsInactive { get; }

    void Act(DirectedAction? action1 = null, DirectedAction? action2 = null);
}
