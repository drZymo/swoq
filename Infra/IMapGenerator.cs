namespace Swoq.Infra;

public interface IMapGenerator
{
    static abstract Map Generate(int level, int height, int width, Random random);

    /// <summary>
    /// Maximum level that can be generated (inclusive bound).
    /// </summary>
    static abstract int MaxLevel { get; }
}
