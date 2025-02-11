using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

internal class OnePlayerCombatTests : GameTestBase
{
    internal static readonly Tile[] InitialSurroundings = ConvertSurroundings(
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "        ######## " +
        "       #p.......#" +
        "       #!....... " +
        "       #........ " +
        "       #........ " +
        "       #+......  " +
        "       #.......  " +
        "       #......   " +
        "       #....     " +
        "        #        ");

    public override void SetUp()
    {
        base.SetUp();
        Assert.That(game.State.Player1, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((1, 1)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.Surroundings, Has.Length.EqualTo(17 * 17));
            Assert.That(game.State.Player1.Surroundings, Is.EqualTo(InitialSurroundings));
        });
    }

    [Test]
    public void SinglePlayerWithoutExtraHealthCannotDefeatEnemy()
    {
        Assert.That(game.State.Player1, Is.Not.Null);

        // Move towards enemy without picking up health
        Assert.That(game.State.Player1.HasSword, Is.False);
        Act(DirectedAction.MoveSouth); // pickup sword
        Assert.That(game.State.Player1.HasSword, Is.True);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 2)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(5));
            Assert.That(changes, Has.Count.EqualTo(25));
            // Player pos changed
            Assert.That(changes[(1, 1)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(5, 2)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Sword picked up
            Assert.That(changes[(2, 1)], Is.EqualTo((Tile.Sword, Tile.Empty)));
            // Some unknown cells became visible
            Assert.That(changes[(2, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(3, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(4, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(5, 8)], Is.EqualTo((Tile.Unknown, Tile.Enemy))); // enemy
            Assert.That(changes[(5, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(6, 8)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(6, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(7, 7)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(7, 8)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(7, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(8, 5)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(8, 6)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(8, 7)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(8, 8)], Is.EqualTo((Tile.Unknown, Tile.Exit))); // exit
            Assert.That(changes[(8, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 2)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 3)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 4)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 5)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 6)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 7)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 8)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
        });

        // Now enemy has seen the player and will move closer
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(5));
            Assert.That(changes, Has.Count.EqualTo(4));
            // Player pos changed
            Assert.That(changes[(5, 2)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(5, 5)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Enemy comes closer
            Assert.That(changes[(5, 8)], Is.EqualTo((Tile.Enemy, Tile.Empty)));
            Assert.That(changes[(5, 6)], Is.EqualTo((Tile.Empty, Tile.Enemy)));
        });

        // Now standing next to enemy, which has not attacked yet.
        // Enemy has 6 health.

        // Attack 4 times, enemy also attacked 4 times
        Act(DirectedAction.UseEast);
        Act(DirectedAction.UseEast);
        Act(DirectedAction.UseEast);
        Act(DirectedAction.UseEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(1)); // health reduced
            Assert.That(changes, Has.Count.EqualTo(0)); // no movements
        });

        // Only 1 health left
        // Attacking now will result in player being attacked back again and die.
        Act(DirectedAction.UseEast);

        // Game is finished now
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.True);
            Assert.That(game.State.Status, Is.EqualTo(GameStatus.FinishedPlayerDied));
            Assert.That(game.State.Player1.Position, Is.EqualTo((-1, -1)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.Health, Is.EqualTo(0));
            Assert.That(changes, Is.Empty);
        });
        Assert.Throws<GameFinishedException>(() => Act(DirectedAction.UseEast));
    }

    [Test]
    public void SinglePlayerWithExtraHealthCanDefeatEnemy()
    {
        Assert.That(game.State.Player1, Is.Not.Null);

        // Move towards enemy without picking up health
        Assert.That(game.State.Player1.HasSword, Is.False);
        Act(DirectedAction.MoveSouth); // pickup sword
        Assert.That(game.State.Player1.HasSword, Is.True);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Assert.That(game.State.Player1.Health, Is.EqualTo(5));
        Act(DirectedAction.MoveSouth); // pickup health
        Assert.That(game.State.Player1.Health, Is.EqualTo(8));
        Act(DirectedAction.MoveEast);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 2)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(8));
            Assert.That(changes, Has.Count.EqualTo(26));
            // Player pos changed
            Assert.That(changes[(1, 1)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(5, 2)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Sword picked up
            Assert.That(changes[(2, 1)], Is.EqualTo((Tile.Sword, Tile.Empty)));
            // Health picked up
            Assert.That(changes[(5, 1)], Is.EqualTo((Tile.Health, Tile.Empty)));
            // Some unknown cells became visible
            Assert.That(changes[(2, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(3, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(4, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(5, 8)], Is.EqualTo((Tile.Unknown, Tile.Enemy))); // enemy
            Assert.That(changes[(5, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(6, 8)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(6, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(7, 7)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(7, 8)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(7, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(8, 5)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(8, 6)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(8, 7)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(8, 8)], Is.EqualTo((Tile.Unknown, Tile.Exit))); // exit
            Assert.That(changes[(8, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 2)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 3)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 4)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 5)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 6)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 7)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 8)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
        });

        // Now enemy has seen the player and will move closer
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(8));
            Assert.That(changes, Has.Count.EqualTo(4));
            // Player pos changed
            Assert.That(changes[(5, 2)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(5, 5)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Enemy comes closer
            Assert.That(changes[(5, 8)], Is.EqualTo((Tile.Enemy, Tile.Empty)));
            Assert.That(changes[(5, 6)], Is.EqualTo((Tile.Empty, Tile.Enemy)));
        });

        // Now standing next to enemy, which has not attacked yet.
        // Enemy has 6 health.

        // Attack 5 times. Enemy also attacks 5 times.
        Act(DirectedAction.UseEast);
        Act(DirectedAction.UseEast);
        Act(DirectedAction.UseEast);
        Act(DirectedAction.UseEast);
        Act(DirectedAction.UseEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(3)); // health reduced
            Assert.That(changes, Has.Count.EqualTo(0)); // no movements
        });

        // Enemy should have 1 health.
        // Attacking now will result in enemy being killed
        Act(DirectedAction.UseEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(3)); // health no longer reduced
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[(5, 6)], Is.EqualTo((Tile.Enemy, Tile.Empty))); // enemy died, removed from game
        });
    }

    [Test]
    public void SecondPlayerActionNotAllowed()
    {
        Assert.Throws<Player2NotPresentException>(() => Act(action2: DirectedAction.MoveWest));
    }

    protected override Map CreateGameMap()
    {
        const int height = 10;
        const int width = 10;
        var map = new MutableMap(0, height, width);
        for (var y = 0; y < height; y++)
        {
            map[y, 0] = Cell.Wall;
            map[y, width - 1] = Cell.Wall;
        }
        for (var x = 1; x < width - 1; x++)
        {
            map[0, x] = Cell.Wall;
            map[height - 1, x] = Cell.Wall;
        }
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                map[y, x] = Cell.Empty;
            }
        }

        map[8, 8] = Cell.Exit;

        map[2, 1] = Cell.Sword;
        map[5, 1] = Cell.Health;

        map.Player1.Position = map.Pos(1, 1);
        map.Enemy1.Position = map.Pos(5, 8);

        return map.ToMap();
    }
}
