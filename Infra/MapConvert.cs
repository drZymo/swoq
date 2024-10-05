using Swoq.Interface;
using System.Collections.Immutable;

namespace Swoq.Infra;

using Position = (int y, int x);

public static class MapConvert
{
    public static TileMap ToOverview(this Map map)
    {
        var tileData = new Tile[map.Height * map.Width];
        var visiblityData = new bool[map.Height * map.Width];

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                tileData[y * map.Width + x] = ToTile(map, (y, x));
                visiblityData[y * map.Width + x] = true;
            }
        }

        return new TileMap(map.Height, map.Width, tileData.ToImmutableArray(), visiblityData.ToImmutableArray());
    }

    public static Tile ToVisibleTile(this Map map, Position tilePos, Position observerPos, int visibilityRange)
    {
        return map.IsVisible(from: observerPos, to: tilePos, maxRange: visibilityRange) ? map.ToTile(tilePos) : Tile.Unknown;
    }

    public static Tile ToTile(this Map map, Position tilePos)
    {
        if ((map.Player1 != null && map.Player1.IsPresent && tilePos.Equals(map.Player1.Position)) ||
            (map.Player2 != null && map.Player2.IsPresent && tilePos.Equals(map.Player2.Position)))
        {
            return Tile.Player;
        }

        foreach (var enemy in map.Enemies.Values)
        {
            if (enemy.IsPresent && tilePos.Equals(enemy.Position))
            {
                return enemy.IsBoss ? Tile.Boss : Tile.Enemy;
            }
        }

        return map[tilePos] switch
        {
            Cell.Unknown => Tile.Unknown,
            Cell.Empty => Tile.Empty,
            Cell.Wall => Tile.Wall,
            Cell.Exit => Tile.Exit,
            Cell.DoorRedClosed => Tile.DoorRed,
            Cell.KeyRed => Tile.KeyRed,
            Cell.DoorGreenClosed => Tile.DoorGreen,
            Cell.KeyGreen => Tile.KeyGreen,
            Cell.DoorBlueClosed => Tile.DoorBlue,
            Cell.KeyBlue => Tile.KeyBlue,
            Cell.PressurePlateRed => Tile.PressurePlateRed,
            Cell.PressurePlateGreen => Tile.PressurePlateGreen,
            Cell.PressurePlateBlue => Tile.PressurePlateBlue,
            Cell.Sword => Tile.Sword,
            Cell.Health => Tile.Health,
            Cell.Treasure => Tile.Treasure,

            Cell.Boulder => Tile.Boulder,
            Cell.PressurePlateRedWithBoulder => Tile.Boulder,
            Cell.PressurePlateGreenWithBoulder => Tile.Boulder,
            Cell.PressurePlateBlueWithBoulder => Tile.Boulder,

            // don't show open doors
            Cell.DoorRedOpen => Tile.Empty,
            Cell.DoorGreenOpen => Tile.Empty,
            Cell.DoorBlueOpen => Tile.Empty,
            _ => throw new NotImplementedException(),
        };
    }
}
