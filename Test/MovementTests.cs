using Swoq.Infra;
using Swoq.Server;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

[TestFixture]
internal class MovementTests : GameTestBase
{
    internal readonly int[] InitialSurroundings1 = ConvertSurroundings(
        "                 " +
        "                 " +
        "    #########    " +
        "   #....@....#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #....pp...#   " +
        "   #....!....#   " +
        "   #....!....#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "    #########    " +
        "                 " +
        "                 ");

    internal readonly int[] InitialSurroundings2 = ConvertSurroundings(
        "                 " +
        "                 " +
        "   #########     " +
        "  #....@....#    " +
        "  #.........#    " +
        "  #.........#    " +
        "  #.........#    " +
        "  #.........#    " +
        "  #....pp...#    " +
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
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));
            Assert.That(game.State.Player1.Surroundings, Has.Length.EqualTo(17 * 17));
            Assert.That(game.State.Player1.Surroundings, Is.EqualTo(InitialSurroundings1));
        });
        Assert.That(game.State.Player2, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2.Position, Is.EqualTo((6, 6)));
            Assert.That(game.State.Player2.Inventory, Is.EqualTo(0));
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
        Act(Server.Action.Move, Direction.South);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));
            Assert.That(game.State.Player1.HasSword, Is.True);
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player moved
            Assert.That(changes[(6, 5)], Is.EqualTo((2, 1)));
            Assert.That(changes[(7, 5)], Is.EqualTo((14, 2)));
        });

        // Another move south would pickup second sword, which is not allowed
        Assert.Throws<InventoryFullException>(() => Act(Server.Action.Move, Direction.South));

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
        Assert.Throws<MoveNotAllowedException>(() => Act(Server.Action.Move, Direction.East));

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
        Act(Server.Action.Move, Direction.North);
        Act(Server.Action.Move, Direction.North);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((4, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));
            Assert.That(changes, Has.Count.EqualTo(4));
            // Player moved
            Assert.That(changes[(6, 5)], Is.EqualTo((2, 1)));
            Assert.That(changes[(4, 5)], Is.EqualTo((1, 2)));
            // Enemy moved
            Assert.That(changes[(1, 5)], Is.EqualTo((15, 1)));
            Assert.That(changes[(3, 5)], Is.EqualTo((1, 15)));
        });

        // Another move north will overlap with enemy, which is not be allowed
        Assert.Throws<MoveNotAllowedException>(() => Act(Server.Action.Move, Direction.North));

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

        // Players in center
        map.Player1.Position = ((height - 1) / 2, (width - 1) / 2);
        map.Player2.Position = ((height - 1) / 2, (width - 1) / 2 + 1);

        // Two swords
        map[map.Player1.Position.y + 1, map.Player1.Position.x] = Cell.Sword;
        map[map.Player1.Position.y + 2, map.Player1.Position.x] = Cell.Sword;

        map.Enemy1.Position = (1, (width - 1) / 2);

        return map.ToMap();
    }
}
