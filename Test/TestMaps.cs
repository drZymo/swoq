using Swoq.Infra;

namespace Swoq.Test;

internal static class TestMaps
{
    public static Map SquareMap { get; } = CreateSquareMap();

    private static Map CreateSquareMap()
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

        map.Player1.Position = (5, 5);

        return map.ToMap();
    }
}
