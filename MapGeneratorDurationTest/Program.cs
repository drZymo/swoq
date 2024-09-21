using Swoq.Infra;

IEnumerable<int> RandomLevels()
{
    var random = new Random();
    while (true)
    {
        yield return random.Next(MapGenerator.MaximumLevel + 1);
    }
}

Parallel.ForEach(RandomLevels(), level =>
{
    MapGenerator.Generate(level, 64, 64);
});
