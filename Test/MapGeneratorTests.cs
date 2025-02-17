using Swoq.Infra;

namespace Swoq.Test;

[TestFixture]
public class MapGeneratorTests
{
    private MapGenerator mapGenerator;

    [SetUp]
    public void SetUp()
    {
        var random = new Random(42);
        mapGenerator = new(64, 64, random);
    }

    private static readonly int[] Levels = Enumerable.Range(0, MapGenerator.MaxLevel + 1).ToArray();

    [TestCaseSource(nameof(Levels))]
    public void GenerateLevel(int level)
    {
        Assert.DoesNotThrow(() => mapGenerator.Generate(level));
    }
}
