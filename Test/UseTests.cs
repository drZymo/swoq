using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

[TestFixture]
internal class UseTests : GameTestBase
{
    internal readonly Tile[] InitialSurroundings1 = ConvertSurroundings(
        "                 " +
        "                 " +
        "    ####e####    " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.....!...#   " +
        "    &...pp..R    " +
        "   #....!r...#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.....+..X#   " +
        "    #########    " +
        "                 " +
        "                 ");

    internal readonly Tile[] InitialSurroundings2 = ConvertSurroundings(
        "                 " +
        "                 " +
        "   ####e####     " +
        "  #.........#    " +
        "  #.........#    " +
        "  #.........#    " +
        "  #.........#    " +
        "  #.....!...#    " +
        "   &...pp..R     " +
        "  #....!r...#    " +
        "  #.........#    " +
        "  #.........#    " +
        "  #.........#    " +
        "  #.....+..X#    " +
        "   #########     " +
        "                 " +
        "                 ");

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();
        Assert.That(game.State.Player1, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((6, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.Surroundings, Has.Length.EqualTo(17 * 17));
            Assert.That(game.State.Player1.Surroundings, Is.EqualTo(InitialSurroundings1));
        });
        Assert.That(game.State.Player2, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2.Position, Is.EqualTo((6, 6)));
            Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player2.Surroundings, Has.Length.EqualTo(17 * 17));
            Assert.That(game.State.Player2.Surroundings, Is.EqualTo(InitialSurroundings2));
        });
    }

    [Test]
    public void UseOnOtherPlayerRequiresSword()
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

        // Use on other player needs a sword
        Assert.Throws<NoSwordException>(() => Act(action1: DirectedAction.UseEast));
        Assert.Throws<NoSwordException>(() => Act(action2: DirectedAction.UseWest));
    }

    [Test]
    public void UseSwordOnOtherPlayerDamagesIt()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
            Assert.That(game.State.Player2, Is.Not.Null);
        });
        Assert.That(game.State.Player1.HasSword, Is.False);

        // Pickup sword with both players
        Act(action1: DirectedAction.MoveSouth);
        Act(action1: DirectedAction.MoveNorth);
        Act(action2: DirectedAction.MoveNorth);
        Act(action2: DirectedAction.MoveSouth);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.HasSword, Is.True);
            Assert.That(game.State.Player2.HasSword, Is.True);
            Assert.That(changes, Has.Count.EqualTo(2));
            // Swords picked up
            Assert.That(changes[(7, 5)], Is.EqualTo((Tile.Sword, Tile.Empty)));
            Assert.That(changes[(5, 6)], Is.EqualTo((Tile.Sword, Tile.Empty)));
        });

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2.Health, Is.EqualTo(5));
            Assert.That(game.State.Player1.Health, Is.EqualTo(5));
        });

        // Use on player 2 will damage it
        Act(action1: DirectedAction.UseEast);
        Assert.That(game.State.Player2.Health, Is.EqualTo(4));

        // Use on player 1 will damage it
        Act(action2: DirectedAction.UseWest);
        Assert.That(game.State.Player1.Health, Is.EqualTo(4));
    }

    [Test]
    public void UseOnEnemyRequiresSword()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });
        Assert.That(game.State.Player1.HasSword, Is.False);

        // Move towards enemy
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((3, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.HasSword, Is.False);
            Assert.That(changes, Has.Count.EqualTo(4));
            // Player moved
            Assert.That(changes[(6, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(3, 5)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Enemy moved
            Assert.That(changes[(0, 5)], Is.EqualTo((Tile.Enemy, Tile.Empty)));
            Assert.That(changes[(2, 5)], Is.EqualTo((Tile.Empty, Tile.Enemy)));
        });

        Assert.Throws<NoSwordException>(() => Act(DirectedAction.UseNorth));

        // Nothing changed
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Health, Is.EqualTo(5)); // Not even player damage
            Assert.That(mapCache.GetNewChanges(), Is.Empty);
        });
    }

    [Test]
    public void UseOnEmptyRequiresInventory()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2, Is.Not.Null);
        });
        Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));

        // Need inventory
        Assert.Throws<InventoryEmptyException>(() => Act(action2: DirectedAction.UseEast));
    }

    [Test]
    public void UseKeyOnKeyyNotAllowed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2, Is.Not.Null);
        });
        Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));

        // Use on key not allowed
        Assert.Throws<UseNotAllowedException>(() => Act(action2: DirectedAction.UseSouth));
    }

    [Test]
    public void UseKeyOnEmptyNotAllowed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2, Is.Not.Null);
        });
        Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));

        // Pickup key
        Act(action2: DirectedAction.MoveSouth);
        Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.KeyRed));

        // Use of key on empty not allowed
        Assert.Throws<UseNotAllowedException>(() => Act(action2: DirectedAction.UseEast));
    }

    [Test]
    public void UseOnDoorNeedsInventory()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2, Is.Not.Null);
        });
        Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));

        // Move to door
        Act(action2: DirectedAction.MoveEast);
        Act(action2: DirectedAction.MoveEast);
        Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));

        // Need inventory
        Assert.Throws<InventoryEmptyException>(() => Act(action2: DirectedAction.UseEast));
    }

    [Test]
    public void UseKeyOnDoorTwiceNotAllowed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2, Is.Not.Null);
        });
        Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));

        // Pickup key
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveNorth);
        Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.KeyRed));
        // Move to door
        Act(action2: DirectedAction.MoveEast);
        Act(action2: DirectedAction.MoveEast);

        // Open door
        Act(action2: DirectedAction.UseEast);

        // Use again not allowed
        Assert.Throws<UseNotAllowedException>(() => Act(action2: DirectedAction.UseEast));
    }

    [Test]
    public void UseOnWallNotAllowed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2, Is.Not.Null);
        });
        Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));

        // Move to wall
        Act(action2: DirectedAction.MoveEast);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2.Position, Is.EqualTo((11, 7)));
            Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player2.HasSword, Is.False);
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player moved
            Assert.That(changes[(6, 6)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(11, 7)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Exit should be south of player
            Assert.That(mapCache[12, 7], Is.EqualTo(Tile.Wall));
        });

        // Use on wall not allowed
        Assert.Throws<UseNotAllowedException>(() => Act(action2: DirectedAction.UseSouth));
    }

    [Test]
    public void UseOnExitNotAllowed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2, Is.Not.Null);
        });
        Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));

        // Move next to exit
        Act(action2: DirectedAction.MoveEast);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveEast);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2.Position, Is.EqualTo((11, 8)));
            Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player2.HasSword, Is.False);
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player moved
            Assert.That(changes[(6, 6)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(11, 8)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Exit should be east of player
            Assert.That(mapCache[11, 9], Is.EqualTo(Tile.Exit));
        });

        // Use on exit not allowed
        Assert.Throws<UseNotAllowedException>(() => Act(action2: DirectedAction.UseEast));
    }

    [Test]
    public void UseOnHealthNotAllowed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2, Is.Not.Null);
        });
        Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));

        // Move next to health
        Act(action2: DirectedAction.MoveEast);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2.Position, Is.EqualTo((11, 7)));
            Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player2.HasSword, Is.False);
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player moved
            Assert.That(changes[(6, 6)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(11, 7)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Health should be west of player
            Assert.That(mapCache[11, 6], Is.EqualTo(Tile.Health));
        });

        // Use on health not allowed
        Assert.Throws<UseNotAllowedException>(() => Act(action2: DirectedAction.UseWest));
    }

    protected override Map CreateGameMap()
    {
        var width = 11;
        var height = 13;
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

        // Two players in center
        map.Player1.Position = map.Pos((height - 1) / 2, (width - 1) / 2);
        map.Player2.Position = map.Pos((height - 1) / 2, (width - 1) / 2 + 1);

        // Swords near players
        map[map.Player1.Position.y + 1, map.Player1.Position.x] = Cell.Sword;
        map[map.Player2.Position.y - 1, map.Player2.Position.x] = Cell.Sword;

        // One enemy in top (in wall)
        map.Enemy1.Position = map.Pos(0, (width - 1) / 2);
        map[map.Enemy1.Position] = Cell.Empty;

        // Boulder in left
        map[map.Player1.Position.y, 1] = Cell.Boulder;

        // A key near player 2
        map[map.Player2.Position.y + 1, map.Player2.Position.x] = Cell.KeyRed;

        // A door on the right
        map[map.Player2.Position.y, width - 2] = Cell.DoorRedClosed;

        // Exit bottom right
        map[height - 2, width - 2] = Cell.Exit;

        // Health below player 2
        map[height - 2, map.Player2.Position.x] = Cell.Health;

        return map.ToMap();
    }
}
