using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

[TestFixture]
internal class InactivityTests : GameTestBase
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

    private DateTime now = DateTime.Now;

    [SetUp]
    public override void SetUp()
    {
        Clock.Setup(() => now);

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
    public void PickupAndPlaceBoulderIsInactivity()
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
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((9, 2)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.Boulder)); // boulder picked up
            Assert.That(changes, Has.Count.EqualTo(5));
            // Player moved
            Assert.That(changes[(5, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(9, 2)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Boulder picked up
            Assert.That(changes[(9, 1)], Is.EqualTo((Tile.Boulder, Tile.Empty)));
            // Map revealed
            Assert.That(changes[(9, 0)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(10, 1)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
        });

        // Continuously pickup and place the boulder (i.e. use) 
        for (var i = 0; i < 500 - 8; i++)
        {
            // Increase time a little
            now += TimeSpan.FromSeconds(2);
            // Place or pickup boulder
            Act(DirectedAction.UseWest);
        }

        // Increase time a little
        now += TimeSpan.FromSeconds(2);
        // Place boulder should now trigger inactivity
        Assert.Throws<GameTimeoutException>(() => Act(DirectedAction.UseWest));
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
