namespace Swoq.Server;

using Position = (int y, int x);

internal static class PositionEx
{
    public static double DistanceTo(this Position position, Position other)
    {
        var dy = position.y - other.y;
        var dx = position.x - other.x;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
