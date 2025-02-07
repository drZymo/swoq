using Swoq.Infra;
using Swoq.Interface;
using Swoq.Server;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

[TestFixture]
internal class CrushTests : GameTestBase
{
    internal readonly Tile[] InitialSurroundings1 = ConvertSurroundings(
        "                 " +
        "                 " +
        "                 " +
        "      #####      " +
        "     R.....B     " +
        "    RR.....BB    " +
        "   #.........#   " +
        "   #.........#   " +
        "   #_...p...-#   " +
        "   #.........#   " +
        "   #.........#   " +
        "   #.......GG    " +
        "    &...=..G     " +
        "     ######      " +
        "                 " +
        "                 " +
        "                 ");

    internal readonly Tile[] InitialSurroundings2 = ConvertSurroundings(
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "                 " +
        "        G        " +
        "       Gp#       " +
        "        #        " +
        "                 " +
        "                 " +
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
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player1.Surroundings, Has.Length.EqualTo(17 * 17));
            Assert.That(game.State.Player1.Surroundings, Is.EqualTo(InitialSurroundings1));
        });
        Assert.That(game.State.Player2, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2.Position, Is.EqualTo((9, 9)));
            Assert.That(game.State.Player2.Inventory, Is.EqualTo(Inventory.None));
            Assert.That(game.State.Player2.Surroundings, Has.Length.EqualTo(17 * 17));
            Assert.That(game.State.Player2.Surroundings, Is.EqualTo(InitialSurroundings2));
        });
    }

    [Test]
    public void DoorCrushesEnemy()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });

        // Pickup boulder and move to plate left
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.UseWest); // pickup
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 2)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.Boulder));
            Assert.That(changes, Has.Count.EqualTo(5));
            // Player moved
            Assert.That(changes[(5, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(5, 2)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Boulder picked up
            Assert.That(changes[(9, 1)], Is.EqualTo((Tile.Boulder, Tile.Empty)));
            // Map revealed
            Assert.That(changes[(9, 0)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(10, 1)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
        });

        // Place boulder on plate
        // Enemy moves towards player immediately
        Act(DirectedAction.UseWest);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 2)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None)); // boulder removed from inventory
            Assert.That(changes, Has.Count.EqualTo(9));
            // Door opened
            Assert.That(changes[(2, 1)], Is.EqualTo((Tile.DoorRed, Tile.Enemy))); // Enemy stepped right in
            Assert.That(changes[(2, 2)], Is.EqualTo((Tile.DoorRed, Tile.Empty)));
            Assert.That(changes[(1, 2)], Is.EqualTo((Tile.DoorRed, Tile.Empty)));
            // Map revealed
            Assert.That(changes[(1, 1)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(1, 0)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(2, 0)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(0, 1)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(0, 2)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            // Boulder on plate
            Assert.That(changes[(5, 1)], Is.EqualTo((Tile.PressurePlateRed, Tile.Boulder))); // boulder on plate
        });

        // Enemy is now at the position of the door.
        // Pick up boulder immediately to close the door.

        // Pick boulder
        Act(DirectedAction.UseWest);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 2)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.Boulder)); // boulder back in inventory
            Assert.That(changes, Has.Count.EqualTo(5));
            // Door closed - (1,2) not in view
            Assert.That(changes[(2, 1)], Is.EqualTo((Tile.Enemy, Tile.DoorRed)));
            Assert.That(changes[(2, 2)], Is.EqualTo((Tile.Empty, Tile.DoorRed)));
            // Boulder from plate
            Assert.That(changes[(5, 1)], Is.EqualTo((Tile.Boulder, Tile.PressurePlateRed)));
            // Key and sword dropped by enemy
            Assert.That(changes[(3, 1)], Is.EqualTo((Tile.Empty, Tile.Sword)));
            Assert.That(changes[(3, 2)], Is.EqualTo((Tile.Empty, Tile.KeyGreen)));
        });

        // Place boulder back on plate
        // Door should be open, and no enemy in view
        Act(DirectedAction.UseWest);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 2)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None)); // boulder removed from inventory
            Assert.That(changes, Has.Count.EqualTo(3));
            // Door opened
            Assert.That(changes[(2, 1)], Is.EqualTo((Tile.DoorRed, Tile.Empty)));
            Assert.That(changes[(2, 2)], Is.EqualTo((Tile.DoorRed, Tile.Empty)));
            // Boulder on plate
            Assert.That(changes[(5, 1)], Is.EqualTo((Tile.PressurePlateRed, Tile.Boulder)));
        });
    }

    [Test]
    public void DoorCrushesBoss()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
        });

        // Pickup boulder and move to plate right
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.UseWest); // pickup
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveNorth);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 8)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.Boulder));
            Assert.That(changes, Has.Count.EqualTo(5));
            // Player moved
            Assert.That(changes[(5, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(5, 8)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Boulder picked up
            Assert.That(changes[(9, 1)], Is.EqualTo((Tile.Boulder, Tile.Empty)));
            // Map revealed
            Assert.That(changes[(9, 0)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(10, 1)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
        });

        // Place boulder on plate
        // Boss moves towards player immediately
        Act(DirectedAction.UseEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 8)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None)); // boulder removed from inventory
            Assert.That(changes, Has.Count.EqualTo(9));
            // Door opened
            Assert.That(changes[(2, 9)], Is.EqualTo((Tile.DoorBlue, Tile.Boss))); // Boss stepped right in
            Assert.That(changes[(2, 8)], Is.EqualTo((Tile.DoorBlue, Tile.Empty)));
            Assert.That(changes[(1, 8)], Is.EqualTo((Tile.DoorBlue, Tile.Empty)));
            // Map revealed
            Assert.That(changes[(1, 9)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(1, 10)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(2, 10)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(0, 9)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(0, 8)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            // Boulder on plate
            Assert.That(changes[(5, 9)], Is.EqualTo((Tile.PressurePlateBlue, Tile.Boulder))); // boulder on plate
        });

        // Boss is now at the position of the door.
        // Pick up boulder immediately to close the door.

        // Pick boulder
        Act(DirectedAction.UseEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 8)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.Boulder)); // boulder back in inventory
            Assert.That(changes, Has.Count.EqualTo(5));
            // Door closed - (1,8) not in view
            Assert.That(changes[(2, 9)], Is.EqualTo((Tile.Boss, Tile.DoorBlue)));
            Assert.That(changes[(2, 8)], Is.EqualTo((Tile.Empty, Tile.DoorBlue)));
            // Boulder from plate
            Assert.That(changes[(5, 9)], Is.EqualTo((Tile.Boulder, Tile.PressurePlateBlue)));
            // Treasures dropped by enemy (key on other side of door)
            Assert.That(changes[(3, 8)], Is.EqualTo((Tile.Empty, Tile.Treasure)));
            Assert.That(changes[(3, 9)], Is.EqualTo((Tile.Empty, Tile.Treasure)));
        });

        // Place boulder back on plate
        // Door should be open, and no enemy in view
        Act(DirectedAction.UseEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((5, 8)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None)); // boulder removed from inventory
            Assert.That(changes, Has.Count.EqualTo(4));
            // Door opened
            Assert.That(changes[(2, 9)], Is.EqualTo((Tile.DoorBlue, Tile.Empty)));
            Assert.That(changes[(2, 8)], Is.EqualTo((Tile.DoorBlue, Tile.Empty)));
            // Key dropped
            Assert.That(changes[(1, 9)], Is.EqualTo((Tile.Empty, Tile.KeyGreen)));
            // Boulder on plate
            Assert.That(changes[(5, 9)], Is.EqualTo((Tile.PressurePlateBlue, Tile.Boulder)));
        });
    }

    [Test]
    public void DoorCrushesPlayer()
    {
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1, Is.Not.Null);
            Assert.That(game.State.Player2, Is.Not.Null);
        });

        // Pickup boulder and move to plate bottom
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.MoveWest);
        Act(DirectedAction.MoveSouth);
        Act(DirectedAction.UseWest); // pickup
        Act(DirectedAction.MoveEast);
        Act(DirectedAction.MoveEast);
        var changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((9, 4)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.Boulder));
            Assert.That(changes, Has.Count.EqualTo(5));
            // Player moved
            Assert.That(changes[(5, 5)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(9, 4)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Boulder picked up
            Assert.That(changes[(9, 1)], Is.EqualTo((Tile.Boulder, Tile.Empty)));
            // Map revealed
            Assert.That(changes[(9, 0)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(10, 1)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
        });

        // Place boulder on plate
        Act(DirectedAction.UseEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player1.Position, Is.EqualTo((9, 4)));
            Assert.That(game.State.Player1.Inventory, Is.EqualTo(Inventory.None)); // boulder removed from inventory
            Assert.That(game.State.Player2.Position, Is.EqualTo((9, 9)));
            Assert.That(changes, Has.Count.EqualTo(6));
            // Door opened
            Assert.That(changes[(8, 9)], Is.EqualTo((Tile.DoorGreen, Tile.Empty)));
            Assert.That(changes[(8, 8)], Is.EqualTo((Tile.DoorGreen, Tile.Empty)));
            Assert.That(changes[(9, 8)], Is.EqualTo((Tile.DoorGreen, Tile.Empty)));
            // Map revealed
            Assert.That(changes[(8, 10)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(10, 8)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            // Boulder on plate
            Assert.That(changes[(9, 5)], Is.EqualTo((Tile.PressurePlateGreen, Tile.Boulder))); // boulder on plate
        });

        // Move player 2 at position of door
        Act(action2: DirectedAction.MoveWest);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.State.Player2.Position, Is.EqualTo((9, 8)));
            Assert.That(changes, Has.Count.EqualTo(2));
            // Player 2 moved
            Assert.That(changes[(9, 9)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(9, 8)], Is.EqualTo((Tile.Empty, Tile.Player)));
        });

        // Pick boulder will close door and kill player 2,
        // ending the game
        Act(DirectedAction.UseEast);
        changes = mapCache.GetNewChanges();
        Assert.Multiple(() =>
        {
            Assert.That(game.IsFinished, Is.True);
            Assert.That(game.State.Status, Is.EqualTo(GameStatus.FinishedPlayer2Died));
            Assert.That(game.State.Player1.Position, Is.EqualTo((9, 4)));
            Assert.That(game.State.Player2.Position, Is.EqualTo((-1, -1)));
            Assert.That(game.State.Player2.Health, Is.EqualTo(0));
            Assert.That(changes, Has.Count.EqualTo(3));
            // Player 2 moved
            Assert.That(changes[(8, 8)], Is.EqualTo((Tile.Empty, Tile.DoorGreen)));
            Assert.That(changes[(9, 5)], Is.EqualTo((Tile.Boulder, Tile.PressurePlateGreen)));
            Assert.That(changes[(9, 8)], Is.EqualTo((Tile.Player, Tile.DoorGreen)));
        });
        Assert.Throws<GameFinishedException>(() => Act(DirectedAction.UseEast));
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

        // Player in center
        map.Player1.Position = ((height - 1) / 2, (width - 1) / 2);

        // Boulder bottom left
        map[height - 2, 1] = Cell.Boulder;

        // Enemy top left with red door
        map.Enemy2.Position = (1, 1);
        map.Enemy2.Inventory = Inventory.KeyGreen;
        map.Enemy2.IsBoss = false;
        map[1, 2] = Cell.DoorRedClosed;
        map[2, 2] = Cell.DoorRedClosed;
        map[2, 1] = Cell.DoorRedClosed;
        // Red plate left
        map[map.Player1.Position.y, 1] = Cell.PressurePlateRed;

        // Boss top right with blue door
        map.Enemy1.Position = (1, width - 2);
        map.Enemy1.Inventory = Inventory.KeyGreen;
        map.Enemy1.IsBoss = true;
        map[1, width - 3] = Cell.DoorBlueClosed;
        map[2, width - 3] = Cell.DoorBlueClosed;
        map[2, width - 2] = Cell.DoorBlueClosed;
        // Blue plate right
        map[map.Player1.Position.y, width - 2] = Cell.PressurePlateBlue;

        // Second player bottom righ right with blue door
        map.Player2.Position = (height - 2, width - 2);
        map[height - 2, width - 3] = Cell.DoorGreenClosed;
        map[height - 3, width - 3] = Cell.DoorGreenClosed;
        map[height - 3, width - 2] = Cell.DoorGreenClosed;
        // Green plate bottom
        map[height - 2, map.Player1.Position.x] = Cell.PressurePlateGreen;

        return map.ToMap();
    }
}
