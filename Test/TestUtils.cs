using Swoq.Interface;

namespace Swoq.Test;

internal static class TestUtils
{
    public static Tile[] ConvertSurroundings(string data)
    {
        static IEnumerable<Tile> Convert(IEnumerable<char> chars)
        {
            foreach (var ch in chars)
            {
                yield return ch switch
                {
                    ' ' => Tile.Unknown,
                    '.' => Tile.Empty,
                    'p' => Tile.Player,
                    '#' => Tile.Wall,
                    'X' => Tile.Exit,
                    'R' => Tile.DoorRed,
                    'r' => Tile.KeyRed,
                    'G' => Tile.DoorGreen,
                    'g' => Tile.KeyGreen,
                    'B' => Tile.DoorBlue,
                    'b' => Tile.KeyBlue,
                    '_' => Tile.PressurePlateRed,
                    '!' => Tile.Sword,
                    '+' => Tile.Health,
                    'e' => Tile.Enemy,
                    'E' => Tile.Boss,
                    '&' => Tile.Boulder,
                    '$' => Tile.Treasure,
                    _ => throw new NotImplementedException()
                };
            }
        }
        return Convert(data).ToArray();
    }
}
