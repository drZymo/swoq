using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

[TestFixture]
internal class MovementTests : GameTestBase
{
    internal readonly Tile[] InitialSurroundings1 = ConvertSurroundings(
        "                 " +
        "                 " +
        "    ####e####    " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "    &...pp...#   " +
        "   #....!r...#   " +
        "   #....!b...#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........X   " +
        "    ##########   " +
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
        "  #.........#    " +
        "   &...pp...#    " +
        "  #....!r...#    " +
        "  #....!b...#    " +
        "  #.........#    " +
        "  #.........#    " +
        "  #.........X    " +
        "   ##########    " +
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
    public void UnknownAction()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });

        // Do an illegal move
        Assert.Throws<UnknownActionException>(() => Act((DirectedAction)(-1)));

        // Nothing changed
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((6, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(changes, Is.Empty);
        });
    }

    [Test]
    public void NoSecondSwordPickupAllowed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });
        Assert.That(game.State.Player1.HasSword, Is.False);

        // Move south to pickup first sword
        Act(DirectedAction.MoveSouth);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.HasSword, Is.True);
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player moved
            Assert.That(changes[(6, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(7, 5)], Is.EqualTo((Tile.Sword, Tile.Player)));
        });

        // Another move south would pickup second sword, which is not allowed
        Assert.Throws<InventoryFullException>(() => Act(DirectedAction.MoveSouth));

        // Nothing changed
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.HasSword, Is.True);
            Assert.That(mapCache.GetNewChanges(), Is.Empty);
        });
    }

    [Test]
    public void NoSecondKeyPickupAllowed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2, Is.Not.Null);
        });
        Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));

        // Move south to pickup first key
        Act(action2: DirectedAction.MoveSouth);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2.Position, Is.EqualTo((7, 6)));
            Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.KeyRed));
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player moved
            Assert.That(changes[(6, 6)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(7, 6)], Is.EqualTo((Tile.KeyRed, Tile.Player)));
        });

        // Another move south would pickup second key, which is not allowed
        Assert.Throws<InventoryFullException>(() => Act(action2: DirectedAction.MoveSouth));

        // Nothing changed
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2.Position, Is.EqualTo((7, 6)));
            Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.KeyRed));
            Assert.That(mapCache.GetNewChanges(), Is.Empty);
        });
    }

    [Test]
    public void PlayersCannotOverlap()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
            Assert.That(game.State.Player2, Is.Not.Null);
        });

        // Move player 1 east so it would overlap with player 2, which is not allowed
        Assert.Throws<MoveNotAllowedException>(() => Act(action1: DirectedAction.MoveEast));
        // Move player 2 west so it would overlap with player 1, which is not allowed
        Assert.Throws<MoveNotAllowedException>(() => Act(action2: DirectedAction.MoveWest));

        // Nothing changed
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((6, 5)));
            Assert.That(game.State.Player2.Position, Is.EqualTo((6, 6)));
            Assert.That(mapCache.GetNewChanges(), Is.Empty);
        });
    }

    [Test]
    public void PlayerAndEnemyCannotOverlap()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });

        // Move towards enemy
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((3, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(changes, Has.Count.EqualTo(4));
            // Player moved
            Assert.That(changes[(6, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(3, 5)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Enemy moved
            Assert.That(changes[(0, 5)], Is.EqualTo((Tile.Enemy, Tile.Empty)));
            Assert.That(changes[(2, 5)], Is.EqualTo((Tile.Empty, Tile.Enemy)));
        });

        // Another move north will overlap with enemy, which is not be allowed
        Assert.Throws<MoveNotAllowedException>(() => Act(DirectedAction.MoveNorth));

        // Nothing changed
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((3, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(mapCache.GetNewChanges(), Is.Empty);
        });
    }

    [Test]
    public void PlayerAndBoulderCannotOverlap()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });

        // Move towards boulder
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveWest);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((6, 2)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player moved
            Assert.That(changes[(6, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(6, 2)], Is.EqualTo((Tile.Empty, Tile.Player)));
        });

        // Another move west will overlap with boulder, which is not allowed
        Assert.Throws<MoveNotAllowedException>(() => Act(DirectedAction.MoveWest));

        // Nothing changed
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((6, 2)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(mapCache.GetNewChanges(), Is.Empty);
        });
    }

    [Test]
    public void Player1ExitsFirst()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
            Assert.That(game.State.Player2, Is.Not.Null);
        });

        // Move to exit with player 1
        Act(action1: DirectedAction.MoveSouth);
        Act(action1: DirectedAction.MoveEast);
        Act(action1: DirectedAction.MoveEast);
        Act(action1: DirectedAction.MoveSouth);
        Act(action1: DirectedAction.MoveSouth);
        Act(action1: DirectedAction.MoveSouth);
        Act(action1: DirectedAction.MoveSouth);
        Act(action1: DirectedAction.MoveEast);
        Act(action1: DirectedAction.MoveEast);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((11, 9)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.KeyRed));
            Assert.That(game.State.Player1.HasSword, Is.True);
            Assert.That(changes, Has.Count.EqualTo(4));
            // Player moved
            Assert.That(changes[(6, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(11, 9)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Sword picked up
            Assert.That(changes[(7, 5)], Is.EqualTo((Tile.Sword, Tile.Empty)));
            // Red key picked up
            Assert.That(changes[(7, 6)], Is.EqualTo((Tile.KeyRed, Tile.Empty)));
        });

        // Move through exit
        Act(action1: DirectedAction.MoveEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((-1, -1)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.KeyRed));
            Assert.That(game.State.Player1.HasSword, Is.True);
            Assert.That(changes, Has.Count.EqualTo(1));
            // Player moved out of game
            Assert.That(changes[(11, 9)], Is.EqualTo((Tile.Player, Tile.Empty)));
        });

        // Another player 1 act not allowed
        Assert.Throws<Player1NotPresentException>(() => Act(action1: DirectedAction.MoveEast));
    }

    [Test]
    public void Player2ExitsFirst()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
            Assert.That(game.State.Player2, Is.Not.Null);
        });

        // Move to exit with player 2
        Act(action2: DirectedAction.MoveEast);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveSouth);
        Act(action2: DirectedAction.MoveEast);
        Act(action2: DirectedAction.MoveEast);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2.Position, Is.EqualTo((11, 9)));
            Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player2.HasSword, Is.False);
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player moved
            Assert.That(changes[(6, 6)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(11, 9)], Is.EqualTo((Tile.Empty, Tile.Player)));
        });

        // Move through exit
        Act(action2: DirectedAction.MoveEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2.Position, Is.EqualTo((-1, -1)));
            Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player2.HasSword, Is.False);
            Assert.That(changes, Has.Count.EqualTo(1));
            // Player moved out of game
            Assert.That(changes[(11, 9)], Is.EqualTo((Tile.Player, Tile.Empty)));
        });

        // Another player 2 act not allowed
        Assert.Throws<Player2NotPresentException>(() => Act(action2: DirectedAction.MoveEast));
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

        // Two swords near player 1
        map[map.Player1.Position.y + 1, map.Player1.Position.x] = Cell.Sword;
        map[map.Player1.Position.y + 2, map.Player1.Position.x] = Cell.Sword;

        // Two swords near player 2
        map[map.Player2.Position.y + 1, map.Player2.Position.x] = Cell.KeyRed;
        map[map.Player2.Position.y + 2, map.Player2.Position.x] = Cell.KeyBlue;

        // One enemy in top (in wall)
        map.Enemy1.Position = map.Pos(0, (width - 1) / 2);
        map[map.Enemy1.Position.y, map.Enemy1.Position.x] = Cell.Empty;

        // Boulder in left
        map[map.Player1.Position.y, 1] = Cell.Boulder;

        // Exit bottom right
        map[height - 2, width - 1] = Cell.Exit;

        return map.ToMap();
    }
}
