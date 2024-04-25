namespace Swoq.Server;

class Quest
{
    private Game currentGame;

    public Quest()
    {
        Id = Guid.NewGuid();
        currentGame = new Game(0);
        CurrentState = currentGame.GetState();
    }

    public Guid Id { get; }
    public int Height => currentGame.Height;
    public int Width => currentGame.Width;

    public GameState CurrentState { get; private set; }

    public void Act(DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        currentGame.Act(action1, action2);
        var state = currentGame.GetState();

        if (state.Finished)
        {
            // TODO: next level is success
        }

        CurrentState = state;
    }
}
