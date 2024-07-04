using Swoq.Infra;
using Swoq.Server;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

[TestFixture('R')]
[TestFixture('G')]
[TestFixture('B')]
internal class DoorTests(char doorColor) : GameTestBase
{
    internal readonly int[] InitialSurroundings = ConvertSurroundings((
        "                 " +
        "                 " +
        "                 " +
        "    ########     " +
        "   #........#    " +
        "   #........#    " +
        "   #........#    " +
        "   #........#    " +
        "   #....p..p#    " +
        "   #........#    " +
        "   #......DD     " +
        "   #....k.D      " +
        "    ######       " +
        "                 " +
        "                 " +
        "                 " +
        "                 ").Replace('D', doorColor).Replace('k', char.ToLower(doorColor)));

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
    public void WithoutKeyDoorCannotBeOpened()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
            Assert.That(game.State.Player2, Is.Not.Null);
        });

        // Move towards door, no change expected except for player itself.

        Act(Server.Action.Move, Direction.South);
        Act(Server.Action.Move, Direction.South);
        Act(Server.Action.Move, Direction.East);

        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 6)));
            Assert.That(game.State.Player2.Position, Is.EqualTo((5, 8)));
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[(5, 5)], Is.EqualTo((2, 1)));
            Assert.That(changes[(7, 6)], Is.EqualTo((1, 2)));
        });

        // Now in front of door
        // No key in inventory
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));

        // Use should fail
        Assert.Throws<InventoryEmptyException>(() => Act(Server.Action.Use, Direction.East));
    }

    [Test]
    public void OpenDoorWithKeys()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
            Assert.That(game.State.Player2, Is.Not.Null);
        });

        // Move towards key, no change expected except for player itself.
        Act(Server.Action.Move, Direction.South);
        Act(Server.Action.Move, Direction.South);

        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(game.State.Player2.Position, Is.EqualTo((5, 8)));
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[(5, 5)], Is.EqualTo((2, 1)));
            Assert.That(changes[(7, 5)], Is.EqualTo((1, 2)));
        });

        // Pickup key
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));
        Act(Server.Action.Move, Direction.South);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((8, 5)));
            Assert.That(game.State.Player2.Position, Is.EqualTo((5, 8)));
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[(7, 5)], Is.EqualTo((2, 1)));
            Assert.That(changes[(8, 5)], Is.EqualTo((KeyValue, 2)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(InventoryValue));
        });

        // Move to door
        Act(Server.Action.Move, Direction.East);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((8, 6)));
            Assert.That(game.State.Player2.Position, Is.EqualTo((5, 8)));
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[(8, 5)], Is.EqualTo((2, 1)));
            Assert.That(changes[(8, 6)], Is.EqualTo((1, 2)));
        });

        // Open door
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(InventoryValue));
        Act(Server.Action.Use, Direction.East);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((8, 6)));
            Assert.That(game.State.Player2.Position, Is.EqualTo((5, 8)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(0)); // Key removed from inventory
            Assert.That(changes, Has.Count.EqualTo(8));
            // Doors opened
            Assert.That(changes[(7, 7)], Is.EqualTo((DoorValue, 1)));
            Assert.That(changes[(7, 8)], Is.EqualTo((DoorValue, 1)));
            Assert.That(changes[(8, 7)], Is.EqualTo((DoorValue, 1)));
            // Exit became visible
            Assert.That(changes[(8, 8)], Is.EqualTo((0, 4)));
            // Some walls became visible
            Assert.That(changes[(7, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(8, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 7)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 8)], Is.EqualTo((0, 3)));
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

        map[8, 7] = DoorCell;
        map[7, 7] = DoorCell;
        map[7, 8] = DoorCell;

        map[8, 5] = KeyCell;

        map.Player1.Position = (5, 5);
        map.Player2.Position = (5, 8);

        return map.ToMap();
    }

    private int DoorValue => doorColor switch
    {
        'R' => 5,
        'G' => 7,
        'B' => 9,
        _ => throw new NotImplementedException(),
    };
    private int KeyValue => doorColor switch
    {
        'R' => 6,
        'G' => 8,
        'B' => 10,
        _ => throw new NotImplementedException(),
    };
    private int InventoryValue => doorColor switch
    {
        'R' => 1,
        'G' => 2,
        'B' => 3,
        _ => throw new NotImplementedException(),
    };
    private Cell DoorCell => doorColor switch
    {
        'R' => Cell.DoorRedClosed,
        'G' => Cell.DoorGreenClosed,
        'B' => Cell.DoorBlueClosed,
        _ => throw new NotImplementedException()
    };
    private Cell KeyCell = doorColor switch
    {
        'R' => Cell.KeyRed,
        'G' => Cell.KeyGreen,
        'B' => Cell.KeyBlue,
        _ => throw new NotImplementedException()
    };
}
