namespace Swoq.Infra;

using Position = (int y, int x);

internal record Room(int Y, int X, int Height, int Width)
{
    private static readonly Random random = new();

    public Position Center => (Y, X);

    public int Top => Y - Height / 2;
    public int Bottom => Top + Height;
    public int Left => X - Width / 2;
    public int Right => Left + Width;

    public Position RandomPosition(int margin)
    {
        var y = random.Next(Top + margin, Bottom - margin);
        var x = random.Next(Left + margin, Right - margin);
        return (y, x);
    }

    public IEnumerable<Position> AllPositions
    {
        get
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    yield return new Position(Top + y, Left + x);
                }
            }
        }
    }
}
