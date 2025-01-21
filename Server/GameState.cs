namespace Swoq.Server;

using Swoq.Interface;
using Position = (int y, int x);

public record PlayerState(Position Position, int Health, Inventory Inventory, bool HasSword, Tile[] Surroundings);

public record GameState(int Tick, int Level, GameStatus Status, PlayerState? Player1 = null, PlayerState? Player2 = null)
{
    public bool IsFinished => Status != GameStatus.Active;
}
