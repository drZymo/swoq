using Swoq.Infra;
using Swoq.Server;

namespace Swoq.Test;

internal class DoorTests : GameTestBase
{
    internal static readonly int[] InitialSurroundings = [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 17
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 34
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 51
        0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, // 68
        0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 85
        0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 102
        0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 119
        0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 136
        0, 0, 0, 3, 1, 1, 1, 1, 2, 1, 1, 2, 3, 0, 0, 0, 0, // 153
        0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 170
        0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 5, 5, 0, 0, 0, 0, 0, // 187
        0, 0, 0, 3, 1, 1, 1, 1, 6, 1, 5, 0, 0, 0, 0, 0, 0, // 204
        0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, 0, 0, // 221
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 238
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 255
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 272
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 289
    ];

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();
        Assert.That(game.State.Player1, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position == (5, 5));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));
            Assert.That(game.State.Player1.Surroundings, Has.Length.EqualTo(17 * 17));
            Assert.That(game.State.Player1.Surroundings, Is.EqualTo(InitialSurroundings));
        });
    }

    [Test]
    public void WithoutKeyDoorCannotBeOpened()
    {
        Assert.That(game.State.Player1, Is.Not.Null);
        Assert.That(game.State.Player2, Is.Not.Null);

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
        Assert.That(game.State.Player1, Is.Not.Null);
        Assert.That(game.State.Player2, Is.Not.Null);

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
            Assert.That(changes[(8, 5)], Is.EqualTo((6, 2)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(1));
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

        map[8, 5] = Cell.KeyRed;

        map.Player1.Position = (5, 5);
        map.Player2.Position = (5, 8);

        return map.ToMap();
    }
}
