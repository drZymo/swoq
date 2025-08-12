namespace Swoq.Infra;

using System.Diagnostics.CodeAnalysis;

public readonly struct Position(int y, int x, int index) : IEquatable<Position>, IComparable<Position>
{
    public int y { get; } = y;
    public int x { get; } = x;
    public int index { get; } = index;

    public readonly bool IsValid => index >= 0;

    public static readonly Position Invalid = new(-1, -1, -1);

    public override readonly bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is Position other && Equals(other);
    }

    public readonly bool Equals(Position other) => index == other.index;

    public override readonly int GetHashCode() => index;

    public readonly int CompareTo(Position other) => other.index - index;

    public readonly double DistanceTo(Position other)
    {
        var dy = y - other.y;
        var dx = x - other.x;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public readonly void Deconstruct(out int y, out int x)
    {
        y = this.y;
        x = this.x;
    }

    public static bool operator ==(Position left, Position right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Position left, Position right)
    {
        return !(left == right);
    }

    public static bool operator <(Position left, Position right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(Position left, Position right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(Position left, Position right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(Position left, Position right)
    {
        return left.CompareTo(right) >= 0;
    }
}
