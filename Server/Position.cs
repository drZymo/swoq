using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Swoc2024Server;

public struct Position : IEquatable<Position>
{
    public Position(IEnumerable<int> values)
    {
        Values = values.ToImmutableArray();
    }

    public IImmutableList<int> Values { get; }

    public readonly int this[int index] => Values[index];

    public override readonly bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj == null) return false;

        return obj is Position other && Equals(other);
    }

    public readonly bool Equals(Position other)
    {
        return Values.SequenceEqual(other.Values);
    }

    public override readonly int GetHashCode()
    {
        var hc = new HashCode();
        foreach (var v in Values)
        {
            hc.Add(v);
        }
        return hc.ToHashCode();
    }
}
