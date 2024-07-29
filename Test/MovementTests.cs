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
        "    #########    " +
        "   #....e....#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "    &...pp...#   " +
        "   #....!....#   " +
        "   #....!....#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "    #########    " +
        "                 " +
        "                 ");

    internal readonly Tile[] InitialSurroundings2 = ConvertSurroundings(
        "                 " +
        "                 " +
        "   #########     " +
        "  #....e....#    " +
        "  #.........#    " +
        "  #.........#    " +
        "  #.........#    " +
        "  #.........#    " +
        "   &...pp...#    " +
        "  #....!....#    " +
        "  #....!....#    " +
        "  #.........#    " +
        "  #.........#    " +
        "  #.........#    " +
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
        Assert.That(mapCache.GetNewChanges(), Is.Empty);
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
        Assert.Throws<MoveNotAllowedException>(() => Act(DirectedAction.MoveEast));

        // Nothing changed
        Assert.That(mapCache.GetNewChanges(), Is.Empty);
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
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((4, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(changes, Has.Count.EqualTo(4));
            // Player moved
            Assert.That(changes[(6, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(4, 5)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Enemy moved
            Assert.That(changes[(1, 5)], Is.EqualTo((Tile.Enemy, Tile.Empty)));
            Assert.That(changes[(3, 5)], Is.EqualTo((Tile.Empty, Tile.Enemy)));
        });

        // Another move north will overlap with enemy, which is not be allowed
        Assert.Throws<MoveNotAllowedException>(() => Act(DirectedAction.MoveNorth));

        // Nothing changed
        Assert.That(mapCache.GetNewChanges(), Is.Empty);
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
        Assert.That(mapCache.GetNewChanges(), Is.Empty);
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
        map.Player1.Position = ((height - 1) / 2, (width - 1) / 2);
        map.Player2.Position = ((height - 1) / 2, (width - 1) / 2 + 1);

        // Two swords near
        map[map.Player1.Position.y + 1, map.Player1.Position.x] = Cell.Sword;
        map[map.Player1.Position.y + 2, map.Player1.Position.x] = Cell.Sword;

        // One enemy in top
        map.Enemy1.Position = (1, (width - 1) / 2);

        // Boulder in left
        map[map.Player1.Position.y, 1] = Cell.Boulder;

        return map.ToMap();
    }
}
