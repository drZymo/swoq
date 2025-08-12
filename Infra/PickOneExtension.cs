namespace Swoq.Infra;

public static class PickOneExtension
{
    public static T PickOne<T>(this IEnumerable<T> values, Random random)
    {
        if (values.Count() == 1)
            return values.First();
        var index = random.Next(0, values.Count());
        return values.Skip(index).First();
    }
}
