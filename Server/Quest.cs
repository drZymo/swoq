using Swoq.Server.Models;
using Swoq.Server.Services;

namespace Swoq.Server;

class Quest
{
    private readonly Player player;
    private readonly ISwoqDatabase database;

    private Game currentGame;

    public Quest(Player player, ISwoqDatabase database)
    {
        this.player = player;
        this.database = database;
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

            if (player.Id != null && player.Level < Level)
            {
                database.UpdatePlayerLevelAsync(player.Id, Level);
                Console.WriteLine($"Player {player.Name} unlocked level {Level}");
                player.Level = Level;
            }
        }

        State = state;
    }
}
