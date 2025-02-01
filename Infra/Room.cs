namespace Swoq.Infra;

using Position = (int y, int x);

internal record Room(int Y, int X, int Height, int Width) : IComparable<Room>
{
    public Position Center => (Y, X);

    public int Top => Y - Height / 2;
    public int Bottom => Top + Height;
    public int Left => X - Width / 2;
    public int Right => Left + Width;

    public IEnumerable<Position> GetPositions(int margin = 0)
    {
        for (var y = Top + margin; y < Bottom - margin; y++)
        {
            for (var x = Left + margin; x < Right - margin; x++)
            {
                yield return (y, x);
            }
        }
    }

    public int CompareTo(Room? other)
    {
        if (other is null) return 1;

        int result = Y.CompareTo(other.Y);
        if (result != 0) return result;

        result = X.CompareTo(other.X);
        if (result != 0) return result;

        result = Height.CompareTo(other.Height);
        if (result != 0) return result;

        return Width.CompareTo(other.Width);
    }
}
