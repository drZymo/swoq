namespace Swoq.Server;

class Quest
{
    private Game currentGame;

    public Quest()
    {
        Id = Guid.NewGuid();
        currentGame = new Game(Level);
        State = currentGame.GetState();
    }

    public Guid Id { get; }
    public int Height => currentGame.Height;
    public int Width => currentGame.Width;

    public int Level { get; private set; } = 0;
    public GameState State { get; private set; }

    public void Act(DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        if (State.Finished) { return; }

        // Play current game
        currentGame.Act(action1, action2);
        var state = currentGame.GetState();

        // If game is completed successfully, then move to next level.
        if (currentGame.Status == GameStatus.Completed)
        {
            Level++;
            currentGame = new Game(Level);
            state = currentGame.GetState();
        }

        State = state;
    }
}
