namespace Swoq.Infra;

public static class PickOneExtension
{
    public static T PickOne<T>(this IEnumerable<T> values)
    {
        if (values.Count() == 1)
            return values.First();
        // Order first before randomizing to make the randomization stable over time.
        // The initial order can be different due to varying HashCode values at each execution.
        // By first ordering the values the order is always the same, making the randomization stable.
        // However this has a slight performance impact.
        return values.Order().OrderBy(_ => Rnd.Next()).First();
    }
}
