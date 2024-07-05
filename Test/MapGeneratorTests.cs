using Swoq.Infra;

namespace Swoq.Test;

[TestFixture]
public class MapGeneratorTests
{
    private MapGenerator mapGenerator;

    [SetUp]
    public void SetUp()
    {
        mapGenerator = new(64, 64);
    }

    private static readonly int[] Levels = Enumerable.Range(0, 14).ToArray();

    [TestCaseSource(nameof(Levels))]
    public void GenerateLevel(int level)
    {
        Assert.DoesNotThrow(() => mapGenerator.Generate(level));
    }
}
