namespace Swoq.Server;

public record GameState(int PlayerX, int PlayerY, int[] Surroundings, bool Finished, int Inventory)
{
    public static readonly GameState Empty = new(-1, -1, [], true, 0);
}
