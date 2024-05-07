namespace Swoq.Server;

using Position = (int y, int x);

public record PlayerState(Position Position, int Health, int Inventory, bool HasSword, int[] Surroundings);

public record GameState(int Tick, int Level, bool Finished, PlayerState? Player1 = null, PlayerState? Player2 = null)
{ }
