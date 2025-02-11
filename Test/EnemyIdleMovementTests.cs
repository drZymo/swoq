using Swoq.Infra;
using Swoq.Interface;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

[TestFixture]
internal class EnemyIdleMovementTests : GameTestBase
{
    internal readonly Tile[] InitialSurroundings1 = ConvertSurroundings(
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "        #        " +
        "       #p#       " +
        "       #.#       " +
        "       #.#       " +
        "       #.#       " +
        "       #.#       " +
        "       #.#       " +
        "       #.#       " +
        "       #.#       " +
        "        .        ");

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();
        Assert.That(game.State.Player1, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((1, 1)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.Surroundings, Has.Length.EqualTo(17 * 17));
            Assert.That(game.State.Player1.Surroundings, Is.EqualTo(InitialSurroundings1));
        });
    }

    [Test]
    public void EnemyMovesTowardsPlayerWhenVisible()
    {
        Assert.That(game.State.Player1, Is.Not.Null);

        // Move south to corner of L
        for (var i = 0; i < 11; i++)
        {
            Act(DirectedAction.MoveSouth);
        }
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((12, 1)));
            Assert.That(changes, Has.Count.EqualTo(30));
            // Player moved
            Assert.That(changes[(1, 1)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(12, 1)], Is.EqualTo((Tile.Unknown, Tile.Player)));
            // Enemy appeared and moved
            Assert.That(changes[(12, 7)], Is.EqualTo((Tile.Unknown, Tile.Enemy)));
            // Rest is map reveal
        });

        // Move towards enemy to trigger it
        // enemy should come closer now
        Act(DirectedAction.MoveEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((12, 2)));
            Assert.That(changes, Has.Count.EqualTo(4));
            // Player moved
            Assert.That(changes[(12, 1)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(12, 2)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Enemy moved
            Assert.That(changes[(12, 7)], Is.EqualTo((Tile.Enemy, Tile.Empty)));
            Assert.That(changes[(12, 6)], Is.EqualTo((Tile.Empty, Tile.Enemy)));
        });
    }

    [Test]
    public void EnemyKeepsMovingOutOfView()
    {
        Assert.That(game.State.Player1, Is.Not.Null);

        // Move south to corner of L
        for (var i = 0; i < 11; i++)
        {
            Act(DirectedAction.MoveSouth);
        }
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((12, 1)));
            Assert.That(changes, Has.Count.EqualTo(30));
            // Player moved
            Assert.That(changes[(1, 1)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(12, 1)], Is.EqualTo((Tile.Unknown, Tile.Player)));
            // Enemy appeared and moved
            Assert.That(changes[(12, 7)], Is.EqualTo((Tile.Unknown, Tile.Enemy)));
            // Rest is map reveal
        });

        // Move towards enemy to trigger it
        // and move back to start position immediately
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveWest);
        for (var i = 0; i < 11; i++)
        {
            Act(DirectedAction.MoveNorth);
        }
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((1, 1)));
            Assert.That(changes, Has.Count.EqualTo(4));
            // Player moved
            Assert.That(changes[(12, 1)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(1, 1)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Enemy moved
            Assert.That(changes[(12, 7)], Is.EqualTo((Tile.Enemy, Tile.Empty)));
            Assert.That(changes[(12, 5)], Is.EqualTo((Tile.Empty, Tile.Enemy))); // Position enemy was seen before turning corner
        });

        // Wait, nothing happens
        Act();
        Act();
        Act();
        Act();
        Act();
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((1, 1)));
            Assert.That(changes, Has.Count.EqualTo(0));
        });

        // Wait, enemy shows up
        Act();
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((1, 1)));
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[(9, 1)], Is.EqualTo((Tile.Empty, Tile.Enemy)));
        });

        // Wait, enemy skips a move
        Act();
        Assert.That(mapCache.GetNewChanges(), Is.Empty);

        // Wait, enemy comes closer
        Act();
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((1, 1)));
            Assert.That(changes, Has.Count.EqualTo(2));
            // Enemy moved
            Assert.That(changes[(9, 1)], Is.EqualTo((Tile.Enemy, Tile.Empty)));
            Assert.That(changes[(8, 1)], Is.EqualTo((Tile.Empty, Tile.Enemy)));
        });
    }

    protected override Map CreateGameMap()
    {
        var width = 9;
        var height = 14;
        var map = new MutableMap(0, height, width);

        // Fill with wall
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                map[y, x] = Cell.Wall;
            }
        }

        // L shaped corridor
        for (var y = 1; y < height - 1; y++)
        {
            map[y, 1] = Cell.Empty;
        }
        for (var x = 1; x < width - 1; x++)
        {
            map[height - 2, x] = Cell.Empty;
        }

        // Player top left
        map.Player1.Position = map.Pos(1, 1);

        // Enemy bottom right
        map.Enemy1.Position = map.Pos(height - 2, width - 2);

        return map.ToMap();
    }
}
