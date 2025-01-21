using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

[TestFixture]
internal class BossTests : GameTestBase
{
    internal readonly Tile[] InitialSurroundings = ConvertSurroundings(
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "        ######## " +
        "       #++++.... " +
        "       #!+++.... " +
        "       #p+++....." +
        "       #.++..... " +
        "       #.++..... " +
        "        ######## " +
        "                 " +
        "                 " +
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
            Assert.That(game.State.Player1.Position, Is.EqualTo((3, 1)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.Surroundings, Has.Length.EqualTo(17 * 17));
            Assert.That(game.State.Player1.Surroundings, Is.EqualTo(InitialSurroundings));
        });
    }

    [Test]
    public void BossInstaKills()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });

        // Pickup sword and health
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        var changes = mapCache.GetNewChanges();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((3, 8)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.HasSword, Is.True); // picked up sword
            Assert.That(game.State.Player1.Health, Is.EqualTo(47)); // +14 health picked up
            Assert.That(changes, Has.Count.EqualTo(66));
            // Player moved
            Assert.That(changes[(3, 1)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(3, 8)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Everything picked up
            Assert.That(changes[(2, 1)], Is.EqualTo((Tile.Sword, Tile.Empty)));
            Assert.That(changes[(1, 1)], Is.EqualTo((Tile.Health, Tile.Empty)));
            Assert.That(changes[(1, 2)], Is.EqualTo((Tile.Health, Tile.Empty)));
            Assert.That(changes[(2, 2)], Is.EqualTo((Tile.Health, Tile.Empty)));
            Assert.That(changes[(3, 2)], Is.EqualTo((Tile.Health, Tile.Empty)));
            Assert.That(changes[(4, 2)], Is.EqualTo((Tile.Health, Tile.Empty)));
            Assert.That(changes[(5, 2)], Is.EqualTo((Tile.Health, Tile.Empty)));
            Assert.That(changes[(5, 3)], Is.EqualTo((Tile.Health, Tile.Empty)));
            Assert.That(changes[(4, 3)], Is.EqualTo((Tile.Health, Tile.Empty)));
            Assert.That(changes[(3, 3)], Is.EqualTo((Tile.Health, Tile.Empty)));
            Assert.That(changes[(2, 3)], Is.EqualTo((Tile.Health, Tile.Empty)));
            Assert.That(changes[(1, 3)], Is.EqualTo((Tile.Health, Tile.Empty)));
            Assert.That(changes[(1, 4)], Is.EqualTo((Tile.Health, Tile.Empty)));
            Assert.That(changes[(2, 4)], Is.EqualTo((Tile.Health, Tile.Empty)));
            Assert.That(changes[(3, 4)], Is.EqualTo((Tile.Health, Tile.Empty)));
            // Rest is just parts of map that appeared
        });

        // Move east will reveal boss
        Act(DirectedAction.MoveEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((3, 9)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.HasSword, Is.True);
            Assert.That(game.State.Player1.Health, Is.EqualTo(47));
            Assert.That(changes, Has.Count.EqualTo(9));
            // Player moved
            Assert.That(changes[(3, 8)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(3, 9)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Map revealed
            Assert.That(changes[(0, 16)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(1, 16)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(2, 16)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(3, 17)], Is.EqualTo((Tile.Unknown, Tile.Boss)));
            Assert.That(changes[(4, 16)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(5, 16)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(6, 16)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
        });

        // Move towards boss
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.False);
            Assert.That(game.State.Player1.Position, Is.EqualTo((3, 14)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.HasSword, Is.True);
            Assert.That(game.State.Player1.Health, Is.EqualTo(47));
            Assert.That(changes, Has.Count.EqualTo(15));
            // Player moved
            Assert.That(changes[(3, 9)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(3, 14)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Boss moved
            Assert.That(changes[(3, 17)], Is.EqualTo((Tile.Boss, Tile.Empty)));
            Assert.That(changes[(3, 15)], Is.EqualTo((Tile.Empty, Tile.Boss)));
            // Rest is map reveal
        });

        // Attack will not work, boss kills in one strike
        Act(DirectedAction.UseEast);

        // Game is finished
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.True);
            Assert.That(game.State.Status, Is.EqualTo(GameStatus.FinishedPlayerDied));
            Assert.That(game.State.Player1.Position, Is.EqualTo((-1, -1)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.Health, Is.EqualTo(0));
            Assert.That(changes, Is.Empty);
        });
        Assert.Throws<GameFinishedException>(() => Act(DirectedAction.UseEast));
    }

    protected override Map CreateGameMap()
    {
        var width = 19;
        var height = 7;
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

        // Player left
        map.Player1.Position = (3, 1);

        // Boss right
        map.Enemy1.Position = (3, (width - 2));
        map.Enemy1.IsBoss = true;

        // Sword and health
        map[2, 1] = Cell.Sword;
        map[1, 1] = Cell.Health;
        map[1, 2] = Cell.Health;
        map[2, 2] = Cell.Health;
        map[3, 2] = Cell.Health;
        map[4, 2] = Cell.Health;
        map[5, 2] = Cell.Health;
        map[5, 3] = Cell.Health;
        map[4, 3] = Cell.Health;
        map[3, 3] = Cell.Health;
        map[2, 3] = Cell.Health;
        map[1, 3] = Cell.Health;
        map[1, 4] = Cell.Health;
        map[2, 4] = Cell.Health;
        map[3, 4] = Cell.Health;


        return map.ToMap();
    }
}
