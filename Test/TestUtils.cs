namespace Swoq.Test;

internal static class TestUtils
{
    public static int[] ConvertSurroundings(string data)
    {
        static IEnumerable<int> Convert(IEnumerable<char> chars)
        {
            foreach (var ch in chars)
            {
                yield return ch switch
                {
                    ' ' => 0,  // unknown
                    '.' => 1,  // empty
                    'p' => 2,  // player
                    '#' => 3,  // wall
                    'X' => 4,  // exit
                    'R' => 5,  // red door
                    'r' => 6,  // red key
                    'G' => 7,  // green door
                    'g' => 8,  // green key
                    'B' => 9,  // blue door
                    'b' => 10, // blue key
                    '=' => 11, // black door
                    '_' => 12, // pressur plate
                    '!' => 13, // sword
                    '@' => 14, // enemy
                    '+' => 15, // health
                    '&' => 16, // boulder
                    _ => throw new NotImplementedException()
                };
            }
        }
        return Convert(data).ToArray();
    }
}
