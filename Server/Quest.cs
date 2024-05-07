using Swoq.Server.Models;
using Swoq.Server.Services;

namespace Swoq.Server;

class Quest : IGame
{
    private readonly Player player;
    private readonly ISwoqDatabase database;
    private readonly DateTime startTime;

    private Game currentGame;
    private int ticks = 0;

    public Quest(Player player, ISwoqDatabase database)
    {
        this.player = player;
        this.database = database;
        this.startTime = DateTime.Now;

        Id = Guid.NewGuid();
        currentGame = new Game(Level);
        State = currentGame.State;
    }

    public Guid Id { get; }
    public DateTime LastAction { get; private set; } = DateTime.Now;

    public int Level { get; private set; } = 0;
    public GameState State { get; private set; }

    public void Act(DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        if (State.Finished) return;

        LastAction = DateTime.Now;
        ticks++;

        // Play current game
        currentGame.Act(action1, action2);
        var state = currentGame.State;

        // If game is completed successfully, then move to next level.
        if (currentGame.Status == GameStatus.Completed)
        {
            Level++;

            // Update player stats
            var lengthTime = LastAction - startTime;
            if (player.Level < Level)
            {
                // If this level was not reached before,
                // unlock the level and remember the length.
                Console.WriteLine($"{player.Name} unlocked level {Level}");
                player.Level = Level;
                player.QuestLengthTicks = ticks;
                player.QuestLengthTime = lengthTime;
            }
            else if (player.Level == Level)
            {
                // If this level was the highest level reached before, then update the duration if it improved.
                player.QuestLengthTicks = ticks < player.QuestLengthTicks ? ticks : player.QuestLengthTicks;
                player.QuestLengthTime = lengthTime < player.QuestLengthTime ? lengthTime : player.QuestLengthTime;
            }

            // Create a new game if this was not the last level
            if (Level <= Parameters.FinalLevel)
            {
                currentGame = new Game(Level);
                state = currentGame.State;
            }
            else
            {
                Console.WriteLine($"{player.Name} finished the quest"); // TODO: Report to dashboard
                state = new GameState(ticks, Level, true);
            }

            // Sync database
            database.UpdatePlayerAsync(player);
        }

        // Overwrite single game ticks with whole quest ticks
        state = state with { Tick = ticks };

        // Update visible state
        State = state;
    }
}
