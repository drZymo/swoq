namespace Swoq.Server;

using Position = (int y, int x);

public record PlayerState(Position Position, int Health, int Inventory, bool HasSword, int[] Surroundings);

public record GameState(PlayerState Player1, PlayerState? Player2, bool Finished)
{
    public static readonly GameState Empty = new(new PlayerState((0, 0), 0, 0, false, []), null, true);
}
