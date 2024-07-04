using Swoq.Infra;
using Swoq.Server;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

internal class TwoPlayerCombatTests : GameTestBase
{
    internal static readonly int[] InitialSurroundings1 = ConvertSurroundings(
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
        "       #+......@ " +
        "       #.......  " +
        "       #!......  " +
        "       #p.....   " +
        "        ####     " +
        "                 ");

    internal static readonly int[] InitialSurroundings2 = ConvertSurroundings(
        "                 " +
        "        ####     " +
        "       #p.....   " +
        "       #!......  " +
        "       #.......  " +
        "       #+......@ " +
        "       #........ " +
        "       #!....... " +
        "       #p......X#" +
        "        ######## " +
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "                 ");

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
            Assert.That(game.State.Player1.Surroundings, Is.EqualTo(InitialSurroundings1));
        });
        Assert.That(game.State.Player2, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2.Position, Is.EqualTo((7, 1)));
            Assert.That(game.State.Player2.Inventory, Is.EqualTo(0));
            Assert.That(game.State.Player2.Surroundings, Has.Length.EqualTo(17 * 17));
            Assert.That(game.State.Player2.Surroundings, Is.EqualTo(InitialSurroundings2));
        });
    }

    [Test]
    public void TwoPlayersCanKillEnemy()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
            Assert.That(game.State.Player2, Is.Not.Null);
        });
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.HasSword, Is.False);
            Assert.That(game.State.Player2.HasSword, Is.False);
        });

        // Move towards enemy without picking up health
        Act(Server.Action.Move, Direction.South, Server.Action.Move, Direction.North); // pickup sword
        Act(Server.Action.Move, Direction.South, Server.Action.Move, Direction.North);
        Act(Server.Action.Move, Direction.East, Server.Action.Move, Direction.East);
        Act(Server.Action.Move, Direction.East, Server.Action.Move, Direction.East);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((3, 3)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(5));
            Assert.That(game.State.Player1.HasSword, Is.True);
            Assert.That(game.State.Player2.Position, Is.EqualTo((5, 3)));
            Assert.That(game.State.Player2.Health, Is.EqualTo(5));
            Assert.That(game.State.Player2.HasSword, Is.True);
            Assert.That(changes, Has.Count.EqualTo(11));
            // Players positions changed
            Assert.That(changes[(1, 1)], Is.EqualTo((2, 1)));
            Assert.That(changes[(3, 3)], Is.EqualTo((1, 2)));
            Assert.That(changes[(7, 1)], Is.EqualTo((2, 1)));
            Assert.That(changes[(5, 3)], Is.EqualTo((1, 2)));
            // Swords picked up
            Assert.That(changes[(2, 1)], Is.EqualTo((13, 1)));
            Assert.That(changes[(6, 1)], Is.EqualTo((13, 1)));
            // Some unknown cells became visible
            Assert.That(changes[(2, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(3, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(4, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(5, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(6, 9)], Is.EqualTo((0, 3)));
        });

        // Now enemy has seen the players and will move closer
        Act(Server.Action.Move, Direction.East, Server.Action.Move, Direction.East);
        Act(Server.Action.Move, Direction.East, Server.Action.Move, Direction.East);
        Act(Server.Action.Move, Direction.East, Server.Action.Move, Direction.East);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((3, 6)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(5));
            Assert.That(game.State.Player2.Position, Is.EqualTo((5, 6)));
            Assert.That(game.State.Player2.Health, Is.EqualTo(5));
            Assert.That(changes, Has.Count.EqualTo(6));
            // Players positions changed
            Assert.That(changes[(3, 3)], Is.EqualTo((2, 1)));
            Assert.That(changes[(3, 6)], Is.EqualTo((1, 2)));
            Assert.That(changes[(5, 3)], Is.EqualTo((2, 1)));
            Assert.That(changes[(5, 6)], Is.EqualTo((1, 2)));
            // Enemy comes closer
            Assert.That(changes[(4, 8)], Is.EqualTo((14, 1)));
            Assert.That(changes[(4, 6)], Is.EqualTo((1, 14)));
        });

        // Now both players standing right next to enemy (north and south of enemy).
        // It has not attacked yet.

        // Attack twice with two players (deals 4 out of 6 damage to enemy)
        Act(Server.Action.Use, Direction.South, Server.Action.Use, Direction.North);
        Act(Server.Action.Use, Direction.South, Server.Action.Use, Direction.North);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((3, 6)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(5)); // player 1 is not attacked
            Assert.That(game.State.Player2.Position, Is.EqualTo((5, 6)));
            Assert.That(game.State.Player2.Health, Is.EqualTo(3)); // player 2 is attacked twice
            Assert.That(changes, Has.Count.EqualTo(0)); // no movements
        });

        // Last attack will deal 2 more damage killing enemy
        Act(Server.Action.Use, Direction.South, Server.Action.Use, Direction.North);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((3, 6)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(5)); // player 1 is no longer attacked
            Assert.That(game.State.Player2.Position, Is.EqualTo((5, 6)));
            Assert.That(game.State.Player2.Health, Is.EqualTo(3)); // player 2 is no longer attacked
            Assert.That(changes, Has.Count.EqualTo(1));
            // Enemy died
            Assert.That(changes[(4, 6)], Is.EqualTo((14, 1)));
        });
    }

    protected override Map CreateGameMap()
    {
        const int height = 9;
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

        map[7, 8] = Cell.Exit;

        map[2, 1] = Cell.Sword;
        map[4, 1] = Cell.Health;
        map[6, 1] = Cell.Sword;

        map.Player1.Position = (1, 1);
        map.Player2.Position = (7, 1);
        map.Enemy1.Position = (4, 8);

        return map.ToMap();
    }
}
