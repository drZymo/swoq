using Swoq.Interface;

namespace Swoq.Server;

internal interface IGame
{
    Guid Id { get; }
    int Level { get; }
    GameState State { get; }
    DateTime LastActionTime { get; }

    void Act(DirectedAction? action1 = null, DirectedAction? action2 = null);

    void CheckGameIsFinished();
}
