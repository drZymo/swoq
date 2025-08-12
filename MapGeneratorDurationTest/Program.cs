using Swoq.Infra;

static IEnumerable<int> RandomLevels(IMapGenerator mapGenerator)
{
    var random = new Random();
    while (true)
    {
        yield return random.Next(mapGenerator.MaxLevel + 1);
    }
}

var mapGenerator = new MapGenerator();

Parallel.ForEach(RandomLevels(mapGenerator), level =>
{
    mapGenerator.Generate(level, 64, 64, new Random());
});
