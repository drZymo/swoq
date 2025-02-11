using Swoq.Infra;
using Swoq.Interface;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

[TestFixture('R')]
[TestFixture('G')]
[TestFixture('B')]
internal class PressurePlateTests(char color) : GameTestBase
{
    internal readonly Tile[] InitialSurroundings = ConvertSurroundings(
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
        "                 ").
        Select(t => t == Tile.DoorRed ? ToDoorTile(color) : t).
        Select(t => t == Tile.PressurePlateRed ? ToPressurePlateTile(color) : t).
        ToArray();

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
            Assert.That(changes[(8, 5)], Is.EqualTo((PressurePlateTile, Tile.Player)));
            // Door has opened
            Assert.That(changes[(8, 7)], Is.EqualTo((DoorTile, Tile.Empty)));
            Assert.That(changes[(7, 7)], Is.EqualTo((DoorTile, Tile.Empty)));
            Assert.That(changes[(7, 8)], Is.EqualTo((DoorTile, Tile.Empty)));
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
            Assert.That(changes[(8, 5)], Is.EqualTo((Tile.Player, PressurePlateTile)));
            Assert.That(changes[(7, 5)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Door (only visible part) has closed
            Assert.That(changes[(8, 7)], Is.EqualTo((Tile.Empty, DoorTile)));
            Assert.That(changes[(7, 7)], Is.EqualTo((Tile.Empty, DoorTile)));
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
            Assert.That(changes[(8, 5)], Is.EqualTo((PressurePlateTile, Tile.Boulder)));
            // Door has opened
            Assert.That(changes[(8, 7)], Is.EqualTo((DoorTile, Tile.Empty)));
            Assert.That(changes[(7, 7)], Is.EqualTo((DoorTile, Tile.Empty)));
            Assert.That(changes[(7, 8)], Is.EqualTo((DoorTile, Tile.Empty)));
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
            Assert.That(changes[(8, 5)], Is.EqualTo((Tile.Boulder, PressurePlateTile)));
            // Door has closed (only visible part)
            Assert.That(changes[(8, 7)], Is.EqualTo((Tile.Empty, DoorTile)));
            Assert.That(changes[(7, 7)], Is.EqualTo((Tile.Empty, DoorTile)));
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

        map.Player1.Position = map.Pos(5, 5);
        map[8, 8] = Cell.Exit;

        var doorCell = color switch
        {
            'R' => Cell.DoorRedClosed,
            'G' => Cell.DoorGreenClosed,
            'B' => Cell.DoorBlueClosed,
            _ => throw new NotImplementedException(),
        };
        map[8, 7] = doorCell;
        map[7, 7] = doorCell;
        map[7, 8] = doorCell;

        var pressurePlateCell = color switch
        {
            'R' => Cell.PressurePlateRed,
            'G' => Cell.PressurePlateGreen,
            'B' => Cell.PressurePlateBlue,
            _ => throw new NotImplementedException(),
        };
        map[8, 5] = pressurePlateCell;

        map[5, 8] = Cell.Boulder;

        return map.ToMap();
    }

    private Tile DoorTile => ToDoorTile(color);

    private static Tile ToDoorTile(char d) => d switch
    {
        'R' => Tile.DoorRed,
        'G' => Tile.DoorGreen,
        'B' => Tile.DoorBlue,
        _ => throw new NotImplementedException(),
    };

    private Tile PressurePlateTile => ToPressurePlateTile(color);

    private static Tile ToPressurePlateTile(char d) => d switch
    {
        'R' => Tile.PressurePlateRed,
        'G' => Tile.PressurePlateGreen,
        'B' => Tile.PressurePlateBlue,
        _ => throw new NotImplementedException(),
    };
}
