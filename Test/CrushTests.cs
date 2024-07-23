using Swoq.Infra;
using Swoq.Server;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

[TestFixture]
internal class CrushTests : GameTestBase
{
    internal readonly int[] InitialSurroundings = ConvertSurroundings(
        "                 " +
        "                 " +
        "                 " +
        "    #### ####    " +
        "   #.... ....#   " +
        "   #....R....#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #..._p....#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #....&....#   " +
        "    #### ####    " +
        "                 " +
        "                 " +
        "                 ");

    [SetUp]
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
    public void DoorCrushesEnemy()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });

        // Go south, pickup boulder and move back
        Act(Server.Action.Move, Direction.South);
        Act(Server.Action.Move, Direction.South);
        Act(Server.Action.Move, Direction.South);
        Act(Server.Action.Use, Direction.South);
        Act(Server.Action.Move, Direction.North);
        Act(Server.Action.Move, Direction.North);
        Act(Server.Action.Move, Direction.North);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(4));
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[(9, 5)], Is.EqualTo((17, 1))); // boulder picked up
            Assert.That(changes[(10, 5)], Is.EqualTo((0, 3))); // hidden wall revealed
        });

        // Place boulder on plate
        // Enemy moves towards player immediately
        Act(Server.Action.Use, Direction.West);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(0)); // boulder removed from inventory
            Assert.That(changes, Has.Count.EqualTo(4));
            Assert.That(changes[(5, 4)], Is.EqualTo((11, 17))); // boulder on plate
            Assert.That(changes[(2, 5)], Is.EqualTo((5, 15))); // door opened and enemy stepped right in
            Assert.That(changes[(1, 5)], Is.EqualTo((0, 1))); // original enemy pos revealed
            Assert.That(changes[(0, 5)], Is.EqualTo((0, 3))); // wall revealed
        });

        // Enemy is now at the position of the door.
        // Pick up boulder immediately to close the door.

        // Pick boulder
        Act(Server.Action.Use, Direction.West);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(4)); // boulder back in inventory
            Assert.That(changes, Has.Count.EqualTo(3));
            Assert.That(changes[(5, 4)], Is.EqualTo((17, 11))); // boulder from plate
            Assert.That(changes[(2, 5)], Is.EqualTo((15, 5))); // door closed on top of enemy
            Assert.That(changes[(3, 5)], Is.EqualTo((1, 14))); // sword dropped
        });

        // Place boulder back on plate
        // Door should be open, and no enemy in view
        Act(Server.Action.Use, Direction.West);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(0)); // boulder removed from inventory
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[(5, 4)], Is.EqualTo((11, 17))); // boulder on plate
            Assert.That(changes[(2, 5)], Is.EqualTo((5, 1))); // door opened, no enemy visible
        });
    }

    protected override Map CreateGameMap()
    {
        var width = 11;
        var height = 11;
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

        // Player in center
        map.Player1.Position = ((height - 1) / 2, (width - 1) / 2);

        // Boulder and pressure plate 
        map[height - 2, map.Player1.Position.x] = Cell.Boulder;
        map[map.Player1.Position.y, map.Player1.Position.x - 1] = Cell.PressurePlateRed;

        // Enemy behind closed door
        map.Enemy1.Position = (1, (width - 1) / 2);
        map[map.Enemy1.Position.y + 1, map.Enemy1.Position.x] = Cell.DoorRedClosed;

        return map.ToMap();
    }
}
