using Swoq.Infra;
using Swoq.Server;

namespace Swoq.Test;

[TestFixture]
internal class PressurePlateTests
{
    private Game game;

    [SetUp]
    public void SetUp()
    {
        game = new Game(CreateSquareMapWithPressurePlateDoorAroundExit(), TimeSpan.FromSeconds(20));
        Assert.That(game.State.Player1, Is.Not.Null);
        Assert.That(game.State.Player1.Position == (5, 5));
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));
        Assert.That(game.State.Player1.Surroundings.Length, Is.EqualTo(17 * 17));
    }

    [Test]
    public void StandOnPlateOpensDoors()
    {
        Assert.That(game.State.Player1, Is.Not.Null);
        Assert.That(game.State.Player1.Surroundings, Is.EqualTo(new int[] {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 17
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 34
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 51
            0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, // 68
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 85
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 102
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 119
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 136
            0, 0, 0, 3, 1, 1, 1, 1, 2, 1, 1,16, 0, 0, 0, 0, 0, // 153
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 170
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1,11,11, 0, 0, 0, 0, 0, // 187
            0, 0, 0, 3, 1, 1, 1, 1,12, 1,11, 0, 0, 0, 0, 0, 0, // 204
            0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, 0, 0, // 221
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 238
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 255
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 272
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 289
        }));

        // Move to plate
        game.Act(new DirectedAction(Server.Action.Move, Direction.South));
        Assert.That(game.State.Player1.Position, Is.EqualTo((6, 5)));
        game.Act(new DirectedAction(Server.Action.Move, Direction.South));
        Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));

        // Player north from plate and doors around exit closed.
        Assert.That(game.State.Player1.Surroundings, Is.EqualTo(new int[] {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 17
            0, 0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, // 34
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 51
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 68
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 85
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, // 102
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1,16, 0, 0, 0, 0, 0, // 119
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, // 136
            0, 0, 0, 3, 1, 1, 1, 1, 2, 1,11, 0, 0, 0, 0, 0, 0, // 153
            0, 0, 0, 3, 1, 1, 1, 1,12, 1,11, 0, 0, 0, 0, 0, 0, // 170
            0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, 0, 0, // 187
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 204
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 221
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 238
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 255
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 272
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 289
        }));

        // Move on plate
        game.Act(new DirectedAction(Server.Action.Move, Direction.South));
        Assert.That(game.State.Player1.Position, Is.EqualTo((8, 5)));

        // Player now on plate and door around exit is open
        Assert.That(game.State.Player1.Surroundings, Is.EqualTo(new int[] {
            0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, // 17
            0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, // 34
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 51
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 68
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, // 85
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1,16, 0, 0, 0, 0, 0, // 102
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 119
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 136
            0, 0, 0, 3, 1, 1, 1, 1, 2, 1, 1, 4, 3, 0, 0, 0, 0, // 153
            0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, // 170
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 187
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 204
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 221
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 238
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 255
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 272
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 289
        }));

        // Move off plate
        game.Act(new DirectedAction(Server.Action.Move, Direction.North));
        Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));

        // Situation back to before.
        // Player north from plate and doors around exit closed.
        Assert.That(game.State.Player1.Surroundings, Is.EqualTo(new int[] {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 17
            0, 0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, // 34
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 51
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 68
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 85
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, // 102
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1,16, 0, 0, 0, 0, 0, // 119
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, // 136
            0, 0, 0, 3, 1, 1, 1, 1, 2, 1,11, 0, 0, 0, 0, 0, 0, // 153
            0, 0, 0, 3, 1, 1, 1, 1,12, 1,11, 0, 0, 0, 0, 0, 0, // 170
            0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, 0, 0, // 187
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 204
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 221
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 238
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 255
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 272
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 289
        }));
    }

    [Test]
    public void BoulderOnPlateOpensDoors()
    {
        Assert.That(game.State.Player1, Is.Not.Null);
        Assert.That(game.State.Player1.Position == (5, 5));
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));
        Assert.That(game.State.Player1.Surroundings.Length, Is.EqualTo(17 * 17));
        Assert.That(game.State.Player1.Surroundings, Is.EqualTo(new int[] {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 17
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 34
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 51
            0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, // 68
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 85
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 102
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 119
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 136
            0, 0, 0, 3, 1, 1, 1, 1, 2, 1, 1,16, 0, 0, 0, 0, 0, // 153
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 170
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1,11,11, 0, 0, 0, 0, 0, // 187
            0, 0, 0, 3, 1, 1, 1, 1,12, 1,11, 0, 0, 0, 0, 0, 0, // 204
            0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, 0, 0, // 221
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 238
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 255
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 272
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 289
        }));

        // Pickup boulders
        game.Act(new DirectedAction(Server.Action.Move, Direction.East));
        Assert.That(game.State.Player1.Position, Is.EqualTo((5, 6)));
        game.Act(new DirectedAction(Server.Action.Move, Direction.East));
        Assert.That(game.State.Player1.Position, Is.EqualTo((5, 7)));
        game.Act(new DirectedAction(Server.Action.Use, Direction.East));
        Assert.That(game.State.Player1.Position, Is.EqualTo((5, 7)));
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(4));

        // Move to plate
        game.Act(new DirectedAction(Server.Action.Move, Direction.West));
        Assert.That(game.State.Player1.Position, Is.EqualTo((5, 6)));
        game.Act(new DirectedAction(Server.Action.Move, Direction.West));
        Assert.That(game.State.Player1.Position, Is.EqualTo((5, 5)));
        game.Act(new DirectedAction(Server.Action.Move, Direction.South));
        Assert.That(game.State.Player1.Position, Is.EqualTo((6, 5)));
        game.Act(new DirectedAction(Server.Action.Move, Direction.South));
        Assert.That(game.State.Player1.Position, Is.EqualTo((7, 5)));

        // Player north from plate,
        // no boulder visible,
        // doors around exit closed.
        Assert.That(game.State.Player1.Surroundings, Is.EqualTo(new int[] {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 17
            0, 0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, // 34
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 51
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 68
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 85
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 102
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 119
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, // 136
            0, 0, 0, 3, 1, 1, 1, 1, 2, 1,11, 0, 0, 0, 0, 0, 0, // 153
            0, 0, 0, 3, 1, 1, 1, 1,12, 1,11, 0, 0, 0, 0, 0, 0, // 170
            0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, 0, 0, // 187
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 204
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 221
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 238
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 255
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 272
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 289
        }));

        // Place boulder
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(4));
        game.Act(new DirectedAction(Server.Action.Use, Direction.South));
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));

        // Boulder now on plate and door around exit is open
        Assert.That(game.State.Player1.Surroundings, Is.EqualTo(new int[] {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 17
            0, 0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, // 34
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 51
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 68
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 85
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 102
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 119
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 136
            0, 0, 0, 3, 1, 1, 1, 1, 2, 1, 1, 1, 3, 0, 0, 0, 0, // 153
            0, 0, 0, 3, 1, 1, 1, 1,16, 1, 1, 4, 3, 0, 0, 0, 0, // 170
            0, 0, 0, 0, 3, 3, 3, 0, 0, 0, 3, 3, 0, 0, 0, 0, 0, // 187
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 204
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 221
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 238
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 255
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 272
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 289
        }));

        // Pick boulder
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(0));
        game.Act(new DirectedAction(Server.Action.Use, Direction.South));
        Assert.That(game.State.Player1.Inventory, Is.EqualTo(4));

        // Situation back to before.
        // Doors around exit closed and plate visible.
        Assert.That(game.State.Player1.Surroundings, Is.EqualTo(new int[] {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 17
            0, 0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, // 34
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 51
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 68
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 85
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 102
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, // 119
            0, 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, // 136
            0, 0, 0, 3, 1, 1, 1, 1, 2, 1,11, 0, 0, 0, 0, 0, 0, // 153
            0, 0, 0, 3, 1, 1, 1, 1,12, 1,11, 0, 0, 0, 0, 0, 0, // 170
            0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, 0, 0, // 187
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 204
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 221
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 238
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 255
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 272
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 289
        }));
    }

    private static Map CreateSquareMapWithPressurePlateDoorAroundExit()
    {
        var map = new MutableMap(0, 10, 10);
        for (var y = 0; y < 10; y++)
        {
            map[y, 0] = Cell.Wall;
            map[y, 9] = Cell.Wall;
        }
        for (var x = 1; x < 9; x++)
        {
            map[0, x] = Cell.Wall;
            map[9, x] = Cell.Wall;
        }

        for (var y = 1; y < 9; y++)
        {
            for (var x = 1; x < 9; x++)
            {
                map[y, x] = Cell.Empty;
            }
        }

        map[8, 8] = Cell.Exit;
        map[8, 7] = Cell.DoorBlackClosed;
        map[7, 7] = Cell.DoorBlackClosed;
        map[7, 8] = Cell.DoorBlackClosed;

        map[8, 5] = Cell.PressurePlate;
        map[5, 8] = Cell.Boulders;

        map.Player1.Position = (5, 5);

        return map.ToMap();
    }
}
