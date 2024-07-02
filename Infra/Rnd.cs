namespace Swoq.Infra;

public static class Rnd
{
    private static Random random = new();

    public static void SetSeed(int seed)
    {
        random = new(seed);
    }

    public static int Next()
    {
        return random.Next();
    }

    public static int Next(int minValue, int maxValue)
    {
        return random.Next(minValue, maxValue);
    }
}
