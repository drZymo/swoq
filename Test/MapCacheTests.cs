using Swoq.Server;

namespace Swoq.Test;

[TestFixture]
public class MapCacheTests
{
    [Test]
    public void SameStateNoChanges()
    {
        MapCache cache = new(8, 8, 2);

        var playerState = new PlayerState(
            (1, 1), 5, 0, false, [
                0, 0, 0, 0, 0,
                0, 3, 3, 3, 3,
                0, 3, 2, 1, 1,
                0, 3, 1, 1, 1,
                0, 3, 1, 1, 1,
            ]);

        cache.AddPlayerStates(playerState, null);
        var changes1 = cache.GetNewChanges();
        Assert.That(changes1, Is.Not.Empty);

        cache.AddPlayerStates(playerState, null);
        var changes2 = cache.GetNewChanges();

        Assert.That(changes2, Is.Empty);
    }

    [Test]
    public void MoveEastAddsColumn()
    {
        MapCache cache = new(8, 8, 2);

        var playerState1 = new PlayerState(
            (1, 1), 5, 0, false, [
                0, 0, 0, 0, 0,
                0, 3, 3, 3, 3,
                0, 3, 2, 1, 1,
                0, 3, 1, 1, 1,
                0, 3, 1, 1, 1,
            ]);

        cache.AddPlayerStates(playerState1, null);
        cache.GetNewChanges();

        var playerState2 = new PlayerState(
            (1, 2), 5, 0, false, [
                0, 0, 0, 0, 0,
                3, 3, 3, 3, 3,
                3, 1, 2, 1, 1,
                3, 1, 1, 1, 1,
                3, 1, 1, 1, 1,
            ]);

        cache.AddPlayerStates(playerState2, null);
        var changes = cache.GetNewChanges();

        // column of 4 and 2 player positions (old and new)
        Assert.Multiple(() =>
        {
            Assert.That(changes, Is.Not.Empty);
            Assert.That(changes, Has.Count.EqualTo(6));
            // Player pos changed
            Assert.That(changes[(1, 1)], Is.EqualTo((2, 1)));
            Assert.That(changes[(1, 2)], Is.EqualTo((1, 2)));
            // Right column appeared
            Assert.That(changes[(0, 4)], Is.EqualTo((0, 3)));
            Assert.That(changes[(1, 4)], Is.EqualTo((0, 1)));
            Assert.That(changes[(2, 4)], Is.EqualTo((0, 1)));
            Assert.That(changes[(3, 4)], Is.EqualTo((0, 1)));
        });
    }

    [Test]
    public void MoveSouthAddsRow()
    {
        MapCache cache = new(8, 8, 2);

        var playerState1 = new PlayerState(
            (1, 1), 5, 0, false, [
                0, 0, 0, 0, 0,
                0, 3, 3, 3, 3,
                0, 3, 2, 1, 1,
                0, 3, 1, 1, 1,
                0, 3, 1, 1, 1,
            ]);
        cache.AddPlayerStates(playerState1, null);
        cache.GetNewChanges();

        var playerState2 = new PlayerState(
            (2, 1), 5, 0, false, [
                0, 3, 3, 3, 3,
                0, 3, 1, 1, 1,
                0, 3, 2, 1, 1,
                0, 3, 1, 1, 1,
                0, 3, 1, 1, 1,
            ]);

        cache.AddPlayerStates(playerState2, null);
        var changes = cache.GetNewChanges();

        // row of 4 and 2 player positions (old and new)
        Assert.Multiple(() =>
        {
            Assert.That(changes, Is.Not.Empty);
            Assert.That(changes, Has.Count.EqualTo(6));

            // Player pos changed
            Assert.That(changes[(1, 1)], Is.EqualTo((2, 1)));
            Assert.That(changes[(2, 1)], Is.EqualTo((1, 2)));

            // Bottom row appeared
            Assert.That(changes[(4, 0)], Is.EqualTo((0, 3)));
            Assert.That(changes[(4, 1)], Is.EqualTo((0, 1)));
            Assert.That(changes[(4, 2)], Is.EqualTo((0, 1)));
            Assert.That(changes[(4, 3)], Is.EqualTo((0, 1)));
        });
    }

    [Test]
    public void UnknownsDoNotOverwrite()
    {
        // All 4 corners are always out of view, so have value unknown.
        // This should not be copied to cache, so no change is expected there.
        MapCache cache = new(8, 8, 2);

        var playerState1 = new PlayerState(
            (1, 1), 5, 0, false, [
                0, 0, 0, 0, 0,
                0, 3, 3, 3, 3,
                0, 3, 2, 1, 1,
                0, 3, 1, 1, 1,
                0, 3, 1, 1, 0,
            ]);

        cache.AddPlayerStates(playerState1, null);
        cache.GetNewChanges();

        // Move south with corner out of view
        var playerState2 = new PlayerState(
            (2, 1), 5, 0, false, [
                0, 3, 3, 3, 0,
                0, 3, 1, 1, 1,
                0, 3, 2, 1, 1,
                0, 3, 1, 1, 1,
                0, 3, 1, 1, 0,
            ]);

        cache.AddPlayerStates(playerState2, null);
        var changes = cache.GetNewChanges();

        // row of 4 and 2 player positions (old and new)
        Assert.Multiple(() =>
        {
            Assert.That(changes, Is.Not.Empty);
            Assert.That(changes, Has.Count.EqualTo(6));

            // Player pos changed
            Assert.That(changes[(1, 1)], Is.EqualTo((2, 1)));
            Assert.That(changes[(2, 1)], Is.EqualTo((1, 2)));

            // Bottom row (excl corner) appeared
            Assert.That(changes[(4, 0)], Is.EqualTo((0, 3)));
            Assert.That(changes[(4, 1)], Is.EqualTo((0, 1)));
            Assert.That(changes[(4, 2)], Is.EqualTo((0, 1)));

            // Earlier invisible bottom right corner became visible
            Assert.That(changes[(3, 3)], Is.EqualTo((0, 1)));
        });
    }

}
