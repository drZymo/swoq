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
    public DateTime LastAction { get; private set; } = DateTime.Now;

    public int Level { get; private set; } = 0;
    public GameState State { get; private set; }

    public void Act(DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        LastAction = DateTime.Now;

        if (State.Finished) { return; }

        // Play current game
        currentGame.Act(action1, action2);
        var state = currentGame.GetState();

        // If game is completed successfully, then move to next level.
        if (currentGame.Status == GameStatus.Completed)
        {
            if (Level < Parameters.FinalLevel)
            {
                Level++;
                currentGame = new Game(Level);
                state = currentGame.GetState();

                if (player.Id != null && player.Level < Level)
                {
                    database.UpdatePlayerLevelAsync(player.Id, Level);
                    Console.WriteLine($"{player.Name} unlocked level {Level}");
                    player.Level = Level;
                }
            }
            else
            {
                Console.WriteLine($"{player.Name} finished the quest");
                state = new GameState(Level, true);
            }
        }

        State = state;
    }
}
