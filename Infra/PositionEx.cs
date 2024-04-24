namespace Swoq.Infra;

using Position = (int y, int x);

public static class PositionEx
{
    public static readonly Position Invalid = new Position(-1, -1);

    public static double DistanceTo(this Position position, Position other)
    {
        var dy = position.y - other.y;
        var dx = position.x - other.x;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static bool IsValid(this Position position)
    {
        return position.y >= 0 && position.x >= 0;
    }
}
