using Swoq.Infra;

namespace Swoq.Test;

[TestFixture]
public class MapGeneratorTests
{
    private static readonly MapGenerator mapGenerator = new();

    private static readonly int[] Levels = Enumerable.Range(0, mapGenerator.MaxLevel + 1).ToArray();

    [TestCaseSource(nameof(Levels))]
    public void GenerateLevel(int level)
    {
        var random = new Random(42);
        Assert.DoesNotThrow(() => mapGenerator.Generate(level, 64, 64, random));
    }
}
