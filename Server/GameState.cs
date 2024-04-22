namespace Swoq.Server;

using Position = (int y, int x);

public record PlayerState(Position Position, int Health, int Inventory, bool HasSword, int[] Surroundings);

public record GameState(PlayerState? Player1, PlayerState? Player2, bool Finished)
{
    public static readonly GameState Empty = new(null, null, true);
}
