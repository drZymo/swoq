namespace Swoq.Server;

internal interface IGame
{
    Guid Id { get; }
    DateTime LastAction { get; }
    GameState State { get; }

    void Act(DirectedAction? action1 = null, DirectedAction? action2 = null);
}
