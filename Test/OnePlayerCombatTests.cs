using Swoq.Infra;
using Swoq.Server;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

internal class OnePlayerCombatTests : GameTestBase
{
    internal static readonly int[] InitialSurroundings = ConvertSurroundings(
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
        // Setup random with seed
        Rnd.SetSeed(1337);

        base.SetUp();
        Assert.That(game.State.Player1, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((1, 1)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));
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
        Act(Server.Action.Move, Direction.South); // pickup sword
        Assert.That(game.State.Player1.HasSword, Is.True);
        Act(Server.Action.Move, Direction.East);
        Act(Server.Action.Move, Direction.South);
        Act(Server.Action.Move, Direction.South);
        Act(Server.Action.Move, Direction.South);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 2)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(5));
            Assert.That(changes, Has.Count.EqualTo(25));
            // Player pos changed
            Assert.That(changes[(1, 1)], Is.EqualTo((2, 1)));
            Assert.That(changes[(5, 2)], Is.EqualTo((1, 2)));
            // Sword picked up
            Assert.That(changes[(2, 1)], Is.EqualTo((13, 1)));
            // Some unknown cells became visible
            Assert.That(changes[(2, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(3, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(4, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(5, 8)], Is.EqualTo((0, 14))); // enemy
            Assert.That(changes[(5, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(6, 8)], Is.EqualTo((0, 1)));
            Assert.That(changes[(6, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(7, 7)], Is.EqualTo((0, 1)));
            Assert.That(changes[(7, 8)], Is.EqualTo((0, 1)));
            Assert.That(changes[(7, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(8, 5)], Is.EqualTo((0, 1)));
            Assert.That(changes[(8, 6)], Is.EqualTo((0, 1)));
            Assert.That(changes[(8, 7)], Is.EqualTo((0, 1)));
            Assert.That(changes[(8, 8)], Is.EqualTo((0, 4))); // exit
            Assert.That(changes[(8, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 2)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 3)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 4)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 5)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 6)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 7)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 8)], Is.EqualTo((0, 3)));
        });

        // Now enemy has seen the player and will move closer
        Act(Server.Action.Move, Direction.East);
        Act(Server.Action.Move, Direction.East);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 4)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(5));
            Assert.That(changes, Has.Count.EqualTo(4));
            // Player pos changed
            Assert.That(changes[(5, 2)], Is.EqualTo((2, 1)));
            Assert.That(changes[(5, 4)], Is.EqualTo((1, 2)));
            // Enemy comes closer
            Assert.That(changes[(5, 8)], Is.EqualTo((14, 1)));
            Assert.That(changes[(5, 6)], Is.EqualTo((1, 14)));
        });

        // Move east will result in standing next to enemy,
        // so enemy will attack immediately,
        // it did not move.
        Act(Server.Action.Move, Direction.East);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(4)); // health reduced
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player pos changed
            Assert.That(changes[(5, 4)], Is.EqualTo((2, 1)));
            Assert.That(changes[(5, 5)], Is.EqualTo((1, 2)));
        });

        // Attack some times, one extra because enemy skipped one action
        Act(Server.Action.Use, Direction.East);
        Act(Server.Action.Use, Direction.East);
        Act(Server.Action.Use, Direction.East);
        Act(Server.Action.Use, Direction.East);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(1)); // health reduced
            Assert.That(changes, Has.Count.EqualTo(0)); // no movements
        });

        // Only 1 health left
        // Attacking now will result in player being attacked also and die.
        Assert.Throws<Player1DiedException>(() => Act(Server.Action.Use, Direction.East));
    }

    [Test]
    public void SinglePlayerWithExtraHealthCannotDefeatEnemy()
    {
        Assert.That(game.State.Player1, Is.Not.Null);

        // Move towards enemy without picking up health
        Assert.That(game.State.Player1.HasSword, Is.False);
        Act(Server.Action.Move, Direction.South); // pickup sword
        Assert.That(game.State.Player1.HasSword, Is.True);
        Act(Server.Action.Move, Direction.South);
        Act(Server.Action.Move, Direction.South);
        Assert.That(game.State.Player1.Health, Is.EqualTo(5));
        Act(Server.Action.Move, Direction.South); // pickup health
        Assert.That(game.State.Player1.Health, Is.EqualTo(8));
        Act(Server.Action.Move, Direction.East);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 2)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(8));
            Assert.That(changes, Has.Count.EqualTo(26));
            // Player pos changed
            Assert.That(changes[(1, 1)], Is.EqualTo((2, 1)));
            Assert.That(changes[(5, 2)], Is.EqualTo((1, 2)));
            // Sword picked up
            Assert.That(changes[(2, 1)], Is.EqualTo((13, 1)));
            // Health picked up
            Assert.That(changes[(5, 1)], Is.EqualTo((15, 1)));
            // Some unknown cells became visible
            Assert.That(changes[(2, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(3, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(4, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(5, 8)], Is.EqualTo((0, 14))); // enemy
            Assert.That(changes[(5, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(6, 8)], Is.EqualTo((0, 1)));
            Assert.That(changes[(6, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(7, 7)], Is.EqualTo((0, 1)));
            Assert.That(changes[(7, 8)], Is.EqualTo((0, 1)));
            Assert.That(changes[(7, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(8, 5)], Is.EqualTo((0, 1)));
            Assert.That(changes[(8, 6)], Is.EqualTo((0, 1)));
            Assert.That(changes[(8, 7)], Is.EqualTo((0, 1)));
            Assert.That(changes[(8, 8)], Is.EqualTo((0, 4))); // exit
            Assert.That(changes[(8, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 2)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 3)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 4)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 5)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 6)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 7)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 8)], Is.EqualTo((0, 3)));
        });

        // Now enemy has seen the player and will move closer
        Act(Server.Action.Move, Direction.East);
        Act(Server.Action.Move, Direction.East);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 4)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(8));
            Assert.That(changes, Has.Count.EqualTo(4));
            // Player pos changed
            Assert.That(changes[(5, 2)], Is.EqualTo((2, 1)));
            Assert.That(changes[(5, 4)], Is.EqualTo((1, 2)));
            // Enemy comes closer
            Assert.That(changes[(5, 8)], Is.EqualTo((14, 1)));
            Assert.That(changes[(5, 6)], Is.EqualTo((1, 14)));
        });

        // Move east will result in standing next to enemy,
        // so enemy will attack immediately,
        // it did not move.
        Act(Server.Action.Move, Direction.East);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(7)); // health reduced
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player pos changed
            Assert.That(changes[(5, 4)], Is.EqualTo((2, 1)));
            Assert.That(changes[(5, 5)], Is.EqualTo((1, 2)));
        });

        // Attack some times
        Act(Server.Action.Use, Direction.East);
        Act(Server.Action.Use, Direction.East);
        Act(Server.Action.Use, Direction.East);
        Act(Server.Action.Use, Direction.East);
        Act(Server.Action.Use, Direction.East);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(3)); // health reduced, one skipped
            Assert.That(changes, Has.Count.EqualTo(0)); // no movements
        });

        // Attacking now will result in enemy being killed
        Act(Server.Action.Use, Direction.East);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(3)); // health no longer reduced
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[(5, 6)], Is.EqualTo((14, 1))); // enemy died
        });
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

        map.Player1.Position = (1, 1);
        map.Enemy1.Position = (5, 8);

        return map.ToMap();
    }
}
