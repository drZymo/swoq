using Swoq.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Swoq.Test;

[TestFixture]
public class MapCacheTests
{
    [Test]
    public void SameStateNoChanges()
    {
        MapCache cache = new(8, 8, 2);

        var playerState = new PlayerState(
            (1, 1), 5, 0, false, new int[] { 
                0, 0, 0, 0, 0,
                0, 3, 3, 3, 3,
                0, 3, 2, 1, 1,
                0, 3, 1, 1, 1,
                0, 3, 1, 1, 1,
            });

        cache.AddPlayerStates(playerState, null);

        var changes = cache.AddPlayerStates(playerState, null);

        Assert.That(changes, Is.Empty);
    }

    [Test]
    public void MoveEastAddsColumn()
    {
        MapCache cache = new(8, 8, 2);

        var playerState1 = new PlayerState(
            (1, 1), 5, 0, false, new int[] {
                0, 0, 0, 0, 0,
                0, 3, 3, 3, 3,
                0, 3, 2, 1, 1,
                0, 3, 1, 1, 1,
                0, 3, 1, 1, 1,
            });

        cache.AddPlayerStates(playerState1, null);

        var playerState2 = new PlayerState(
            (1, 2), 5, 0, false, new int[] {
                0, 0, 0, 0, 0,
                3, 3, 3, 3, 3,
                3, 1, 2, 1, 1,
                3, 1, 1, 1, 1,
                3, 1, 1, 1, 1,
            });

        var changes = cache.AddPlayerStates(playerState2, null);
        Assert.That(changes, Is.Not.Empty);

        // column of 4 and 2 player positions (old and new)
        Assert.That(changes.Count, Is.EqualTo(6));

        Assert.That(changes[(0, 4)], Is.EqualTo((0, 3)));
        Assert.That(changes[(1, 4)], Is.EqualTo((0, 1)));
        Assert.That(changes[(2, 4)], Is.EqualTo((0, 1)));
        Assert.That(changes[(3, 4)], Is.EqualTo((0, 1)));
        
        Assert.That(changes[(1, 1)], Is.EqualTo((2, 1)));
        Assert.That(changes[(1, 2)], Is.EqualTo((1, 2)));
    }
}
