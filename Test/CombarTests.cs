using Swoq.Infra;
using Swoq.Server;

namespace Swoq.Test;

internal class CombatTests : GameTestBase
{
    internal static readonly int[] InitialSurroundings = [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 17
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 34
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 51
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 68
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 85
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 102
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 119
        0, 0, 0, 0, 0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 3, 0, // 136
        0, 0, 0, 0, 0, 0, 0, 3, 2, 1, 1, 1, 1, 1, 1, 1, 3, // 153
        0, 0, 0, 0, 0, 0, 0, 3,13, 1, 1, 1, 1, 1, 1, 1, 0, // 170
        0, 0, 0, 0, 0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 0, // 187
        0, 0, 0, 0, 0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 0, // 204
        0, 0, 0, 0, 0, 0, 0, 3,15, 1, 1, 1, 1, 1, 1, 0, 0, // 221
        0, 0, 0, 0, 0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 0, 0, // 238
        0, 0, 0, 0, 0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 0, 0, 0, // 255
        0, 0, 0, 0, 0, 0, 0, 3, 1, 1, 1, 1, 0, 0, 0, 0, 0, // 272
        0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, // 289
    ];

    public override void SetUp()
    {
        base.SetUp();
        Assert.That(game.State.Player1, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((1, 1)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));
            Assert.That(game.State.Player1.Surroundings, Has.Length.EqualTo(17 * 17));
            Assert.That(game.State.Player1.Surroundings, Is.EqualTo(InitialSurroundings));
        });
    }

    [Test]
    public void SinglePlayerWithoutExtraHealthCannotDefeatEnemy()
    {
        Assert.That(game.State.Player1, Is.Not.Null);

        // Move towards enemy without picking up health
        Assert.That(game.State.Player1.HasSword, Is.False);
        Act(Server.Action.Move, Direction.South); // pickup sword
        Assert.That(game.State.Player1.HasSword, Is.True);
        Act(Server.Action.Move, Direction.East);
        Act(Server.Action.Move, Direction.South);
        Act(Server.Action.Move, Direction.East);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((3, 3)));
            Assert.That(game.State.Player1.Health, Is.EqualTo(5));
            Assert.That(changes, Has.Count.EqualTo(25));
            // Player pos changed
            Assert.That(changes[(1, 1)], Is.EqualTo((2, 1)));
            Assert.That(changes[(3, 3)], Is.EqualTo((1, 2)));
            // Sword picked up
            Assert.That(changes[(2, 1)], Is.EqualTo((13, 1)));
            // Some unknown cells became visible
            Assert.That(changes[(2, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(3, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(4, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(5, 8)], Is.EqualTo((0, 14))); // enemy
            Assert.That(changes[(5, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(6, 8)], Is.EqualTo((0, 1)));
            Assert.That(changes[(6, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(7, 7)], Is.EqualTo((0, 1)));
            Assert.That(changes[(7, 8)], Is.EqualTo((0, 1)));
            Assert.That(changes[(7, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(8, 5)], Is.EqualTo((0, 1)));
            Assert.That(changes[(8, 6)], Is.EqualTo((0, 1)));
            Assert.That(changes[(8, 7)], Is.EqualTo((0, 1)));
            Assert.That(changes[(8, 8)], Is.EqualTo((0, 4))); // exit
            Assert.That(changes[(8, 9)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 2)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 3)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 4)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 5)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 6)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 7)], Is.EqualTo((0, 3)));
            Assert.That(changes[(9, 8)], Is.EqualTo((0, 3)));
        });

        // TODO: next move enemy comes closer
        // TODO: Fix random seed
    }

    protected override Map CreateGameMap()
    {
        const int height = 10;
        const int width = 10;
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

        map[8, 8] = Cell.Exit;

        map[2, 1] = Cell.Sword;
        map[5, 1] = Cell.Health;

        map.Player1.Position = (1, 1);
        map.Enemy1.Position = (5, 8);

        return map.ToMap();
    }
}
