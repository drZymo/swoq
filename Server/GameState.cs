namespace Swoq.Server;

using Position = (int y, int x);

public record PlayerState(Position Position, int[] Surroundings, int Inventory, bool HasSword);

public record GameState(PlayerState Player1, bool Finished)
{
    public static readonly GameState Empty = new(new PlayerState((0,0), [], 0, false), true);
}
