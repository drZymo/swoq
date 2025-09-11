using Swoq.Interface;
using System.Collections.Immutable;

namespace Swoq.Infra;

public static class MapConvert
{
    public static TileMap ToOverview(this Map map)
    {
        var tileData = new Tile[map.Height * map.Width];
        var visiblityData = new bool[map.Height * map.Width];

        for (var i = 0; i < map.Height * map.Width; i++)
        {
            tileData[i] = ToTile(map, i);
            visiblityData[i] = true;
        }

        return new TileMap(map.Height, map.Width, tileData.ToImmutableArray(), visiblityData.ToImmutableArray());
    }

    public static Tile ToVisibleTile(this Map map, Position tilePos, Position observerPos, int visibilityRange)
    {
        return map.IsVisible(from: observerPos, to: tilePos, maxRange: visibilityRange) ? map.ToTile(tilePos.index) : Tile.Unknown;
    }

    private static readonly Dictionary<Cell, Tile> CellToTileMapping = new Dictionary<Cell, Tile>{
        { Cell.Unknown, Tile.Unknown } ,
        { Cell.Empty, Tile.Empty } ,
        { Cell.Wall, Tile.Wall },
        { Cell.Exit, Tile.Exit },
        { Cell.DoorRedClosed, Tile.DoorRed },
        { Cell.KeyRed, Tile.KeyRed },
        { Cell.DoorGreenClosed, Tile.DoorGreen },
        { Cell.KeyGreen, Tile.KeyGreen },
        { Cell.DoorBlueClosed, Tile.DoorBlue },
        { Cell.KeyBlue, Tile.KeyBlue },
        { Cell.PressurePlateRed, Tile.PressurePlateRed },
        { Cell.PressurePlateGreen, Tile.PressurePlateGreen },
        { Cell.PressurePlateBlue, Tile.PressurePlateBlue },
        { Cell.Sword, Tile.Sword },
        { Cell.Health, Tile.Health },
        { Cell.Treasure, Tile.Treasure },

        // only show the boulder on top
        { Cell.Boulder, Tile.Boulder },
        { Cell.PressurePlateRedWithBoulder, Tile.Boulder },
        { Cell.PressurePlateGreenWithBoulder, Tile.Boulder },
        { Cell.PressurePlateBlueWithBoulder, Tile.Boulder },

        // don't show open doors
        { Cell.DoorRedOpen, Tile.Empty },
        { Cell.DoorGreenOpen, Tile.Empty },
        { Cell.DoorBlueOpen, Tile.Empty },
    };

    private static Tile ToTile(this Map map, int tileIndex)
    {
        if (map.Player1Position.index == tileIndex || map.Player2Position.index == tileIndex)
        {
            return Tile.Player;
        }

        if (map.Enemy1 != null && map.Enemy1.IsPresent && map.Enemy1.Position.index == tileIndex) return map.Enemy1.IsBoss ? Tile.Boss : Tile.Enemy;
        if (map.Enemy2 != null && map.Enemy2.IsPresent && map.Enemy2.Position.index == tileIndex) return map.Enemy2.IsBoss ? Tile.Boss : Tile.Enemy;
        if (map.Enemy3 != null && map.Enemy3.IsPresent && map.Enemy3.Position.index == tileIndex) return map.Enemy3.IsBoss ? Tile.Boss : Tile.Enemy;

        return CellToTileMapping.TryGetValue(map[tileIndex], out var tile) ? tile : throw new NotImplementedException();
    }
}
