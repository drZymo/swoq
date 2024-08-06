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
    public void ActiveAfterAct()
    {
        var game = new Game(TestMaps.SquareMap, TimeSpan.FromSeconds(20));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });

        now += TimeSpan.FromSeconds(10);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });

        Assert.DoesNotThrow(() => game.Act(DirectedAction.MoveEast));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });

        now += TimeSpan.FromSeconds(9);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });

        Assert.DoesNotThrow(() => game.Act(DirectedAction.MoveEast));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });

        now += TimeSpan.FromSeconds(2);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });
    }

    [Test]
    public void InactiveAfterNoAction()
    {
        var game = new Game(TestMaps.SquareMap, TimeSpan.FromSeconds(20));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });

        Assert.DoesNotThrow(() => game.Act(DirectedAction.MoveEast));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.True);
        });

        now += TimeSpan.FromSeconds(21);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.CheckIsActive(), Is.False);
        });

        Assert.Throws<NoProgressException>(() => game.Act(DirectedAction.MoveEast));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.True);
            Assert.That(game.CheckIsActive(), Is.False);
        });

        Assert.Throws<GameFinishedException>(() => game.Act(DirectedAction.MoveEast));

        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.True);
            Assert.That(game.CheckIsActive(), Is.False);
        });
    }

    [Test]
    public void PickupAndPlaceBoulderIsInactivity()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });

        // Pickup boulder
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
        Assert.Throws<NoProgressException>(() => Act(DirectedAction.UseWest));
    }

    [Test]
    public void WalkingAroundPickingUpBouldersIsActivity()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });

        // Pickup boulder
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

        // Repeat walking around in circles, picking up and placing boulders.
        // Should never fail.
        for (var j = 0; j < 100; j++)
        {
            // Place and pickup boulder to trigger map changes
            Act(DirectedAction.UseWest);
            Act(DirectedAction.UseWest);
            WalkCircle();
        }
    }

    [Test]
    public void OnlyWalkingAroundIsNoProgress()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });

        // Walk around
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveWest);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((9, 2)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player moved
            Assert.That(changes[(5, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(9, 2)], Is.EqualTo((Tile.Empty, Tile.Player)));
        });

        // Repeat walking around in circles for a while, not picking up anything
        // Results in no progress
        for (var j = 0; j < (1000 - 7) / 28; j++)
        {
            WalkCircle();
        }

        // One more circle will trigger no progress
        Assert.Throws<NoProgressException>(() => WalkCircle());
    }

    [Test]
    public void OnlyWalkingAroundInSmallCirtcleIsInactivity()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });

        // Walk around
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveWest);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((9, 2)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player moved
            Assert.That(changes[(5, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(9, 2)], Is.EqualTo((Tile.Empty, Tile.Player)));
        });

        // Repeat walking around in circles for a while, not picking up anything
        // Results in no progress
        for (var j = 0; j < (500 - 7) / 16; j++)
        {
            WalkCircle(small: true);
            // Full circle, nothing changed
            Assert.That(mapCache.GetNewChanges(), Is.Empty);
        }

        // One more circle will trigger no progress
        Assert.Throws<NoProgressException>(() => WalkCircle(small: true));
    }

    private void WalkCircle(bool small = false)
    {
        int vertical = (small ? 4 : 8);
        int horizontal = (small ? 4 : 6);
        for (var i = 0; i < vertical; i++)
        {
            Act(DirectedAction.MoveNorth);
        }
        for (var i = 0; i < horizontal; i++)
        {
            Act(DirectedAction.MoveEast);
        }
        for (var i = 0; i < vertical; i++)
        {
            Act(DirectedAction.MoveSouth);
        }
        for (var i = 0; i < horizontal; i++)
        {
            Act(DirectedAction.MoveWest);
        }
        // Full circle, nothing changed
        Assert.That(mapCache.GetNewChanges(), Is.Empty);
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
