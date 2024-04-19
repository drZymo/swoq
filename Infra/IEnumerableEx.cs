namespace Swoq.Infra;

public static class IEnumerableEx
{
    private static readonly Random random = new();

    public static T PickOne<T>(this IEnumerable<T> values)
    {
        return values.OrderBy(_ => random.Next()).First();
    }
}
