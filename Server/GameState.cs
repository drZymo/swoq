namespace Swoq.Server;

public record GameState(int playerX, int playerY, int[] Surroundings, bool Finished, int Inventory)
{
    public static readonly GameState Empty = new GameState(-1, -1, [], true, 0);
}
