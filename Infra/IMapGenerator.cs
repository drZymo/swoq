namespace Swoq.Infra;

internal class MapGeneratorException(string message) : Exception(message) { }

public interface IMapGenerator
{
    Map Generate(int level, int height, int width, Random random);

    /// <summary>
    /// Maximum level that can be generated (inclusive bound).
    /// </summary>
    int MaxLevel { get; }
}
