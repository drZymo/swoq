using Swoq.Infra;
using Swoq.Server;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

internal class PressurePlateTests : GameTestBase
{
    internal static readonly int[] InitialSurroundings = ConvertSurroundings(
        "                 " +
        "                 " +
        "                 " +
        "    ########     " +
        "   #........#    " +
        "   #........#    " +
        "   #........#    " +
        "   #........#    " +
        "   #....p..&     " +
        "   #........#    " +
        "   #......RR     " +
        "   #...._.R      " +
        "    ######       " +
        "                 " +
        "                 " +
        "                 " +
        "                 ");

    public override void SetUp()
    {
        base.SetUp();
        Assert.That(game.State.Player1, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));
            Assert.That(game.State.Player1.Surroundings, Has.Length.EqualTo(17 * 17));
            Assert.That(game.State.Player1.Surroundings, Is.EqualTo(InitialSurroundings));
        });
    }

    [Test]
    public void StandOnPlateOpensDoors()
    {
        Assert.That(game.State.Player1, Is.Not.Null);

        // Move towards plate, no change expected except for player itself.
        Act(Server.Action.Move, Direction.South);
        Act(Server.Action.Move, Direction.South);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[(5, 5)], Is.EqualTo((2, 1)));
            Assert.That(changes[(7, 5)], Is.EqualTo((1, 2)));
        });

        // Player north from plate and doors around exit closed.

        // Move on plate
        Act(Server.Action.Move, Direction.South);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((8, 5)));
            Assert.That(changes, Has.Count.EqualTo(10));
            // Player pos has changed
            Assert.That(changes[(7, 5)], Is.EqualTo((2, 1)));
            Assert.That(changes[(8, 5)], Is.EqualTo((11, 2)));
            // Door has opened
            Assert.That(changes[(8, 7)], Is.EqualTo((5, 1)));
            Assert.That(changes[(7, 7)], Is.EqualTo((5, 1)));
            Assert.That(changes[(7, 8)], Is.EqualTo((5, 1)));
            // Exit appeared
            Assert.That(changes[(8, 8)], Is.EqualTo((0, 4)));
            // Walls around exit appeared
            Assert.That(changes[(7, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(8, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 8)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 7)], Is.EqualTo((0, 3)));
        });

        // Move off plate
        Act(Server.Action.Move, Direction.North);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            // Situation back to before.
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(changes, Has.Count.EqualTo(4));
            // Player pos has changed
            Assert.That(changes[(8, 5)], Is.EqualTo((2, 11)));
            Assert.That(changes[(7, 5)], Is.EqualTo((1, 2)));
            // Door (only visible part) has closed
            Assert.That(changes[(8, 7)], Is.EqualTo((1, 5)));
            Assert.That(changes[(7, 7)], Is.EqualTo((1, 5)));
        });
    }

    [Test]
    public void BoulderOnPlateOpensDoors()
    {
        Assert.That(game.State.Player1, Is.Not.Null);

        // Move besides boulder
        Act(Server.Action.Move, Direction.East);
        Act(Server.Action.Move, Direction.East);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 7)));
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player pos changed
            Assert.That(changes[(5, 5)], Is.EqualTo((2, 1)));
            Assert.That(changes[(5, 7)], Is.EqualTo((1, 2)));
            Assert.That(changes, Has.Count.EqualTo(2));
        });

        // Pickup boulder
        Act(Server.Action.Use, Direction.East);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 7)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(4));
            Assert.That(changes, Has.Count.EqualTo(2));
            // Boulder removed
            Assert.That(changes[(5, 8)], Is.EqualTo((17, 1)));
            // Wall behind visible
            Assert.That(changes[(5, 9)], Is.EqualTo((0, 3)));
        });

        // Move besides plate
        Act(Server.Action.Move, Direction.West);
        Act(Server.Action.Move, Direction.West);
        Act(Server.Action.Move, Direction.South);
        Act(Server.Action.Move, Direction.South);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player pos changed
            Assert.That(changes[(5, 7)], Is.EqualTo((2, 1)));
            Assert.That(changes[(7, 5)], Is.EqualTo((1, 2)));
        });

        // Player north from plate,
        // no boulder visible,
        // doors around exit still closed.

        // Place boulder
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(4));
        Act(Server.Action.Use, Direction.South);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(changes, Has.Count.EqualTo(9));
            // Plate changed
            Assert.That(changes[(8, 5)], Is.EqualTo((11, 17)));
            // Door has opened
            Assert.That(changes[(8, 7)], Is.EqualTo((5, 1)));
            Assert.That(changes[(7, 7)], Is.EqualTo((5, 1)));
            Assert.That(changes[(7, 8)], Is.EqualTo((5, 1)));
            // Exit appeared
            Assert.That(changes[(8, 8)], Is.EqualTo((0, 4)));
            // Walls around exit appeared
            Assert.That(changes[(7, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(8, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 8)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 7)], Is.EqualTo((0, 3)));
        });

        // Pick boulder
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));
        Act(Server.Action.Use, Direction.South);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(4));
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(changes, Has.Count.EqualTo(3));
            // Plate changed
            Assert.That(changes[(8, 5)], Is.EqualTo((17, 11)));
            // Door has closed (only visible part)
            Assert.That(changes[(8, 7)], Is.EqualTo((1, 5)));
            Assert.That(changes[(7, 7)], Is.EqualTo((1, 5)));
        });
    }

    protected override Map CreateGameMap()
    {
        var map = new MutableMap(0, 10, 10);
        for (var y = 0; y < 10; y++)
        {
            map[y, 0] = Cell.Wall;
            map[y, 9] = Cell.Wall;
        }
        for (var x = 1; x < 9; x++)
        {
            map[0, x] = Cell.Wall;
            map[9, x] = Cell.Wall;
        }

        for (var y = 1; y < 9; y++)
        {
            for (var x = 1; x < 9; x++)
            {
                map[y, x] = Cell.Empty;
            }
        }

        map[8, 8] = Cell.Exit;
        map[8, 7] = Cell.DoorRedClosed;
        map[7, 7] = Cell.DoorRedClosed;
        map[7, 8] = Cell.DoorRedClosed;

        map[8, 5] = Cell.PressurePlateRed;
        map[5, 8] = Cell.Boulder;

        map.Player1.Position = (5, 5);

        return map.ToMap();
    }
}
