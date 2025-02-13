namespace Swoq.Infra;

public static class PickOneExtension
{
    public static T PickOne<T>(this IEnumerable<T> values)
    {
        if (values.Count() == 1)
            return values.First();
        return values.OrderBy(_ => Rnd.Next()).First();
    }
}
