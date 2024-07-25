using Swoq.Interface;
using static Swoq.Test.TestUtils;

namespace Swoq.Test;

[TestFixture]
public class MapCacheTests
{
    [Test]
    public void SameStateNoChanges()
    {
        // Initial state
        MapCache cache = new(8, 8, 2);
        var playerState = new Server.PlayerState(
            (1, 1), 5, 0, false, ConvertSurroundings(
                "     " +
                " ####" +
                " #p.." +
                " #..." +
                " #..."));
        cache.AddPlayerStates(playerState, null);
        var changes1 = cache.GetNewChanges();
        Assert.That(changes1, Is.Not.Empty);

        // Repeat with same state
        cache.AddPlayerStates(playerState, null);
        var changes2 = cache.GetNewChanges();
        // Nothing changed
        Assert.That(changes2, Is.Empty);
    }

    [Test]
    public void MoveEastAddsColumn()
    {
        // Initial state
        MapCache cache = new(8, 8, 2);
        var playerState1 = new Server.PlayerState(
            (1, 1), 5, 0, false, ConvertSurroundings(
                "     " +
                " ####" +
                " #p.." +
                " #..." +
                " #..."));
        cache.AddPlayerStates(playerState1, null);
        cache.GetNewChanges();

        // Add move east
        var playerState2 = new Server.PlayerState(
            (1, 2), 5, 0, false, ConvertSurroundings(
                "     " +
                "#####" +
                "#.p.." +
                "#...." +
                "#...."));
        cache.AddPlayerStates(playerState2, null);
        var changes = cache.GetNewChanges();

        // column of 4 and 2 player positions (old and new)
        Assert.Multiple(() =>
        {
            Assert.That(changes, Is.Not.Empty);
            Assert.That(changes, Has.Count.EqualTo(6));
            // Player pos changed
            Assert.That(changes[(1, 1)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(1, 2)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Right column appeared
            Assert.That(changes[(0, 4)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(1, 4)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(2, 4)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(3, 4)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
        });
    }

    [Test]
    public void MoveSouthAddsRow()
    {
        // Initial state
        MapCache cache = new(8, 8, 2);
        var playerState1 = new Server.PlayerState(
            (1, 1), 5, 0, false, ConvertSurroundings(
                "     " +
                " ####" +
                " #p.." +
                " #..." +
                " #..."));
        cache.AddPlayerStates(playerState1, null);
        cache.GetNewChanges();

        // Add move south
        var playerState2 = new Server.PlayerState(
            (2, 1), 5, 0, false, ConvertSurroundings(
                " ####" +
                " #..." +
                " #p.." +
                " #..." +
                " #..."));
        cache.AddPlayerStates(playerState2, null);
        var changes = cache.GetNewChanges();

        // row of 4 and 2 player positions (old and new)
        Assert.Multiple(() =>
        {
            Assert.That(changes, Is.Not.Empty);
            Assert.That(changes, Has.Count.EqualTo(6));

            // Player pos changed
            Assert.That(changes[(1, 1)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(2, 1)], Is.EqualTo((Tile.Empty, Tile.Player)));

            // Bottom row appeared
            Assert.That(changes[(4, 0)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(4, 1)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(4, 2)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(4, 3)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
        });
    }

    [Test]
    public void UnknownsDoNotOverwrite()
    {
        // All 4 corners are always out of view, so have value unknown.
        // This should not be copied to cache, so no change is expected there.

        // Initial state
        MapCache cache = new(8, 8, 2);
        var playerState1 = new Server.PlayerState(
            (1, 1), 5, 0, false, ConvertSurroundings(
                "     " +
                " ####" +
                " #p.." +
                " #..." +
                " #.. ")); // bottom right corner is unknown
        cache.AddPlayerStates(playerState1, null);
        cache.GetNewChanges();

        // Move south with corner out of view
        var playerState2 = new Server.PlayerState(
            (2, 1), 5, 0, false, ConvertSurroundings(
                " ### " +
                " #..." +
                " #p.." +
                " #..." +
                " #.. ")); // top and bottom right corners are unknown
        cache.AddPlayerStates(playerState2, null);
        var changes = cache.GetNewChanges();

        // row of 4 and 2 player positions (old and new)
        Assert.Multiple(() =>
        {
            Assert.That(changes, Is.Not.Empty);
            Assert.That(changes, Has.Count.EqualTo(6));

            // Player pos changed
            Assert.That(changes[(1, 1)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(2, 1)], Is.EqualTo((Tile.Empty, Tile.Player)));

            // Bottom row (excl corner) appeared
            Assert.That(changes[(4, 0)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(4, 1)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(4, 2)], Is.EqualTo((Tile.Unknown, Tile.Empty)));

            // Earlier invisible bottom right corner became visible
            Assert.That(changes[(3, 3)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
        });
    }

    [Test]
    public void TwoMovesEastAddTwoColumn()
    {
        // Initial state
        MapCache cache = new(8, 8, 2);
        var playerState1 = new Server.PlayerState(
            (1, 1), 5, 0, false, ConvertSurroundings(
                "     " +
                " ####" +
                " #p.." +
                " #..." +
                " #..."));
        cache.AddPlayerStates(playerState1, null);
        cache.GetNewChanges();

        // Move east twice
        var playerState2 = new Server.PlayerState(
            (1, 2), 5, 0, false, ConvertSurroundings(
                "     " +
                "#####" +
                "#.p.." +
                "#...." +
                "#...."));
        cache.AddPlayerStates(playerState2, null);
        var playerState3 = new Server.PlayerState(
            (1, 3), 5, 0, false, ConvertSurroundings(
                "     " +
                "#####" +
                "..p.#" +
                "....#" +
                "....#"));
        cache.AddPlayerStates(playerState3, null);

        var changes = cache.GetNewChanges();

        // Two columns of 4 and 2 player positions (old and new)
        Assert.Multiple(() =>
        {
            Assert.That(changes, Is.Not.Empty);
            Assert.That(changes, Has.Count.EqualTo(10));
            // Player pos changed
            Assert.That(changes[(1, 1)], Is.EqualTo((Tile.Player, Tile.Empty)));
            Assert.That(changes[(1, 3)], Is.EqualTo((Tile.Empty, Tile.Player)));
            // Column 1 appeared
            Assert.That(changes[(0, 4)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(1, 4)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(2, 4)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            Assert.That(changes[(3, 4)], Is.EqualTo((Tile.Unknown, Tile.Empty)));
            // Column 2 appeared
            Assert.That(changes[(0, 5)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(1, 5)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(2, 5)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
            Assert.That(changes[(3, 5)], Is.EqualTo((Tile.Unknown, Tile.Wall)));
        });
    }

}
