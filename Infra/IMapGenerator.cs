namespace Swoq.Infra;

public interface IMapGenerator
{
    Map Generate(int level);

    /// <summary>
    /// Maximum level that can be generated (inclusive bound).
    /// </summary>
    int MaxLevel { get; }
}
