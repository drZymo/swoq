namespace Swoq.Infra;

public static class IEnumerableEx
{
    public static T PickOne<T>(this IEnumerable<T> values)
    {
        return values.OrderBy(_ => Rnd.Next()).First();
    }
}
