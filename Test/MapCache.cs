using Swoq.Server;
using System.Collections.Immutable;
using Position = System.ValueTuple<int, int>;

namespace Swoq.Test;

internal class MapCache(int height, int width, int visibilityRange)
{
    private int[] cache = new int[height * width];

    public int this[int y, int x]
    {
        get
        {
            if (y < 0 || y >= height) throw new ArgumentOutOfRangeException(nameof(y));
            if (x < 0 || x >= width) throw new ArgumentOutOfRangeException(nameof(x));
            return cache[y * width + x];
        }
    }

    public IImmutableDictionary<Position, (int from, int to)> AddPlayerStates(
        PlayerState? playerState1, PlayerState? playerState2)
    {
        var changes = ImmutableDictionary<Position, (int from, int to)>.Empty;

        AddOnePlayerState(playerState1, ref changes);
        AddOnePlayerState(playerState2, ref changes);

        return changes;
    }

    private void AddOnePlayerState(PlayerState? playerState, ref ImmutableDictionary<(int, int), (int from, int to)> changes)
    {
        if (playerState == null) return;

        var top = playerState.Position.y - visibilityRange;
        var left = playerState.Position.x - visibilityRange;
        var surroundingsSize = visibilityRange * 2 + 1;

        for (int sy = 0; sy < surroundingsSize; sy++)
        {
            var my = top + sy;
            for (int sx = 0; sx < surroundingsSize; sx++)
            {
                var mx = left + sx;

                if (0 <= my && my < height && 0 <= mx && mx < width)
                {
                    var newValue = playerState.Surroundings[sy * surroundingsSize + sx];
                    var cacheIndex = my * width + mx;
                    if (cache[cacheIndex] != newValue)
                    {
                        changes = changes.SetItem((my, mx), (cache[cacheIndex], newValue));
                        cache[cacheIndex] = newValue;
                    }
                }
            }
        }
    }
}
