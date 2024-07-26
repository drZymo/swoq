using Swoq.Infra;
using Swoq.Interface;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

[TestFixture]
internal class CrushTests : GameTestBase
{
    internal readonly Tile[] InitialSurroundings = ConvertSurroundings(
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
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
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
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.UseSouth);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.Boulder));
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[(9, 5)], Is.EqualTo((Tile.Boulder, Tile.Empty))); // boulder picked up
            Assert.That(changes[(10, 5)], Is.EqualTo((Tile.Unknown, Tile.Wall))); // hidden wall revealed
        });

        // Place boulder on plate
        // Enemy moves towards player immediately
        Act(DirectedAction.UseWest);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None)); // boulder removed from inventory
            Assert.That(changes, Has.Count.EqualTo(4));
            Assert.That(changes[(5, 4)], Is.EqualTo((Tile.PressurePlateRed, Tile.Boulder))); // boulder on plate
            Assert.That(changes[(2, 5)], Is.EqualTo((Tile.DoorRed, Tile.Enemy))); // door opened and enemy stepped right in
            Assert.That(changes[(1, 5)], Is.EqualTo((Tile.Unknown, Tile.Empty))); // original enemy pos revealed
            Assert.That(changes[(0, 5)], Is.EqualTo((Tile.Unknown, Tile.Wall))); // wall revealed
        });

        // Enemy is now at the position of the door.
        // Pick up boulder immediately to close the door.

        // Pick boulder
        Act(DirectedAction.UseWest);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.Boulder)); // boulder back in inventory
            Assert.That(changes, Has.Count.EqualTo(3));
            Assert.That(changes[(5, 4)], Is.EqualTo((Tile.Boulder, Tile.PressurePlateRed))); // boulder from plate
            Assert.That(changes[(2, 5)], Is.EqualTo((Tile.Enemy, Tile.DoorRed))); // door closed on top of enemy
            Assert.That(changes[(3, 5)], Is.EqualTo((Tile.Empty, Tile.Sword))); // sword dropped
        });

        // Place boulder back on plate
        // Door should be open, and no enemy in view
        Act(DirectedAction.UseWest);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None)); // boulder removed from inventory
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[(5, 4)], Is.EqualTo((Tile.PressurePlateRed, Tile.Boulder))); // boulder on plate
            Assert.That(changes[(2, 5)], Is.EqualTo((Tile.DoorRed, Tile.Empty))); // door opened, no enemy visible
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
