using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

[TestFixture]
internal class ExitTests : GameTestBase
{
    internal readonly Tile[] InitialSurroundings = ConvertSurroundings(
        "                 " +
        "                 " +
        "                 " +
        "    #########    " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #....p....#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.........#   " +
        "    &.......X#   " +
        "     ########    " +
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
    public void ExitNormally()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });

        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False); // Game not finished yet
            Assert.That(game.State.Player1.Position, Is.EqualTo((8, 9)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player moved
            Assert.That(changes[(5, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(8, 9)], Is.EqualTo((Tile.Empty, Tile.Player)));
        });

        // Move through exit
        Act(DirectedAction.MoveSouth);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.True); // Game finished
            Assert.That(game.State.Player1.Position, Is.EqualTo((-1, -1))); // Player is now off the map
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.Health, Is.EqualTo(5)); // Health has not changed
            Assert.That(game.State.Player1.Surroundings, Is.Empty); // No more surroundings
            Assert.That(changes, Is.Empty); // No more map updates
    });

        // No more interactions allowed
        Assert.Throws<GameFinishedException>(() => Act(DirectedAction.MoveNorth));
    }

    [Test]
    public void ExitWithBoulderNotAllowed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });

        // Pickup boulder and move to exit
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.UseWest); // Pickup boulder
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((9, 8)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.Boulder)); // boulder picked up
            Assert.That(changes, Has.Count.EqualTo(5));
            // Player moved
            Assert.That(changes[(5, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(9, 8)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Boulder picked up
            Assert.That(changes[(9, 1)], Is.EqualTo((Tile.Boulder, Tile.Empty)));
            // Map revealed
            Assert.That(changes[(9, 0)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(10, 1)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
        });

        // Move through exit should result in player death
        Assert.Throws<Player1DiedException>(() => Act(DirectedAction.MoveEast));

        // Game is finished
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.True);
            Assert.That(game.State.Player1.Position, Is.EqualTo((-1, -1)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(0));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(changes, Is.Empty);
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

        // Player center
        map.Player1.Position = (height / 2, width / 2);

        // Exit bottom right
        map[height - 2, width - 2] = Cell.Exit;

        // Some items to pickup
        map[height - 2, 1] = Cell.Boulder;

        return map.ToMap();
    }
}
