using Swoq.Infra;
using Swoq.Interface;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

internal class PressurePlateTests : GameTestBase
{
    internal static readonly Tile[] InitialSurroundings = ConvertSurroundings(
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
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.Surroundings, Has.Length.EqualTo(17 * 17));
            Assert.That(game.State.Player1.Surroundings, Is.EqualTo(InitialSurroundings));
        });
    }

    [Test]
    public void StandOnPlateOpensDoors()
    {
        Assert.That(game.State.Player1, Is.Not.Null);

        // Move towards plate, no change expected except for player itself.
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[(5, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(7, 5)], Is.EqualTo((Tile.Empty, Tile.Player)));
        });

        // Player north from plate and doors around exit closed.

        // Move on plate
        Act(DirectedAction.MoveSouth);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((8, 5)));
            Assert.That(changes, Has.Count.EqualTo(10));
            // Player pos has changed
            Assert.That(changes[(7, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(8, 5)], Is.EqualTo((Tile.PressurePlateRed, Tile.Player)));
            // Door has opened
            Assert.That(changes[(8, 7)], Is.EqualTo((Tile.DoorRed, Tile.Empty)));
            Assert.That(changes[(7, 7)], Is.EqualTo((Tile.DoorRed, Tile.Empty)));
            Assert.That(changes[(7, 8)], Is.EqualTo((Tile.DoorRed, Tile.Empty)));
            // Exit appeared
            Assert.That(changes[(8, 8)], Is.EqualTo((Tile.Unknown, Tile.Exit)));
            // Walls around exit appeared
            Assert.That(changes[(7, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(8, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 8)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 7)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
        });

        // Move off plate
        Act(DirectedAction.MoveNorth);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            // Situation back to before.
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(changes, Has.Count.EqualTo(4));
            // Player pos has changed
            Assert.That(changes[(8, 5)], Is.EqualTo((Tile.Player, Tile.PressurePlateRed)));
            Assert.That(changes[(7, 5)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Door (only visible part) has closed
            Assert.That(changes[(8, 7)], Is.EqualTo((Tile.Empty, Tile.DoorRed)));
            Assert.That(changes[(7, 7)], Is.EqualTo((Tile.Empty, Tile.DoorRed)));
        });
    }

    [Test]
    public void BoulderOnPlateOpensDoors()
    {
        Assert.That(game.State.Player1, Is.Not.Null);

        // Move besides boulder
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 7)));
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player pos changed
            Assert.That(changes[(5, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(5, 7)], Is.EqualTo((Tile.Empty, Tile.Player)));
            Assert.That(changes, Has.Count.EqualTo(2));
        });

        // Pickup boulder
        Act(DirectedAction.UseEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 7)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.Boulder));
            Assert.That(changes, Has.Count.EqualTo(2));
            // Boulder removed
            Assert.That(changes[(5, 8)], Is.EqualTo((Tile.Boulder, Tile.Empty)));
            // Wall behind visible
            Assert.That(changes[(5, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
        });

        // Move besides plate
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player pos changed
            Assert.That(changes[(5, 7)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(7, 5)], Is.EqualTo((Tile.Empty, Tile.Player)));
        });

        // Player north from plate,
        // no boulder visible,
        // doors around exit still closed.

        // Place boulder
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.Boulder));
        Act(DirectedAction.UseSouth);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(changes, Has.Count.EqualTo(9));
            // Plate changed
            Assert.That(changes[(8, 5)], Is.EqualTo((Tile.PressurePlateRed, Tile.Boulder)));
            // Door has opened
            Assert.That(changes[(8, 7)], Is.EqualTo((Tile.DoorRed, Tile.Empty)));
            Assert.That(changes[(7, 7)], Is.EqualTo((Tile.DoorRed, Tile.Empty)));
            Assert.That(changes[(7, 8)], Is.EqualTo((Tile.DoorRed, Tile.Empty)));
            // Exit appeared
            Assert.That(changes[(8, 8)], Is.EqualTo((Tile.Unknown, Tile.Exit)));
            // Walls around exit appeared
            Assert.That(changes[(7, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(8, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 8)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(9, 7)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
        });

        // Pick boulder
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
        Act(DirectedAction.UseSouth);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.Boulder));
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(changes, Has.Count.EqualTo(3));
            // Plate changed
            Assert.That(changes[(8, 5)], Is.EqualTo((Tile.Boulder, Tile.PressurePlateRed)));
            // Door has closed (only visible part)
            Assert.That(changes[(8, 7)], Is.EqualTo((Tile.Empty, Tile.DoorRed)));
            Assert.That(changes[(7, 7)], Is.EqualTo((Tile.Empty, Tile.DoorRed)));
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
