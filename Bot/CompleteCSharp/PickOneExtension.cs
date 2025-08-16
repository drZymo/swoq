namespace Bot;

public static class PickOneExtension
{
    public static T PickOne<T>(this IEnumerable<T> values)
    {
        if (values.Count() == 1)
            return values.First();
        var index = Random.Shared.Next(0, values.Count());
        return values.Skip(index).First();
    }
}
