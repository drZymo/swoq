using Swoq.Server;
using System.Collections.Immutable;

namespace Swoq.Test;

using Tile = Swoq.Interface.Tile;

internal class MapCache(int height, int width, int visibilityRange)
{
    private readonly Tile[] cache = new Tile[height * width];
    private ImmutableDictionary<(int, int), (Tile from, Tile to)> changes = ImmutableDictionary<(int, int), (Tile from, Tile to)>.Empty;

    public Tile this[int y, int x]
    {
        get
        {
            if (y < 0 || y >= height) throw new ArgumentOutOfRangeException(nameof(y));
            if (x < 0 || x >= width) throw new ArgumentOutOfRangeException(nameof(x));
            return cache[y * width + x];
        }
    }

    public void AddPlayerStates(
        PlayerState? playerState1, PlayerState? playerState2)
    {
        AddPlayerState(playerState1);
        AddPlayerState(playerState2);
    }

    public ImmutableDictionary<(int, int), (Tile from, Tile to)> GetNewChanges()
    {
        var currentChanges = changes;
        changes = ImmutableDictionary<(int, int), (Tile from, Tile to)>.Empty;
        return currentChanges;
    }

    private void AddPlayerState(PlayerState? playerState)
    {
        if (playerState == null) return;

        var top = playerState.Position.y - visibilityRange;
        var left = playerState.Position.x - visibilityRange;
        var surroundingsSize = visibilityRange * 2 + 1;

        if (playerState.Surroundings.Length > 0)
        {
            for (int sy = 0; sy < surroundingsSize; sy++)
            {
                var my = top + sy;
                for (int sx = 0; sx < surroundingsSize; sx++)
                {
                    var mx = left + sx;

                    if (0 <= my && my < height && 0 <= mx && mx < width)
                    {
                        var newValue = playerState.Surroundings[sy * surroundingsSize + sx];
                        Change(my, mx, newValue);
                    }
                }
            }
        }
    }

    private void Change(int y, int x, Tile newTile)
    {
        if (newTile == 0) return;

        var cacheIndex = y * width + x;

        if (cache[cacheIndex] == newTile) return;

        var oldValue = cache[cacheIndex];
        cache[cacheIndex] = newTile;

        var key = (y, x);
        if (changes.TryGetValue(key, out var oldChange))
        {
            if (newTile == oldChange.from)
            {
                // Earlier changes undone, so remove it
                changes = changes.Remove(key);
            }
            else
            {
                // Earlier changes overwritten
                changes = changes.SetItem(key, (oldChange.from, newTile));
            }
        }
        else
        {
            // New change
            changes = changes.Add(key, (oldValue, newTile));
        }
    }
}
