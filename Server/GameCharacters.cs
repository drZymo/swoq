using Swoq.Infra;

namespace Swoq.Server;

using Position = (int y, int x);

internal enum GameCharacterId
{
    Player1,
    Player2,
    Enemy1,
    Enemy2,
}

internal abstract record GameCharacter(
    GameCharacterId Id,
    Position Position,
    Inventory Inventory,
    int Health);

internal record GamePlayer(
    GameCharacterId Id,
    Position Position,
    Inventory Inventory = Inventory.None,
    int Health = Parameters.PlayerHealth,
    bool HasSword = false)
    : GameCharacter(Id, Position, Inventory, Health);

internal record GameEnemy(
    GameCharacterId Id,
    Position Position,
    Inventory Inventory = Inventory.None,
    int Health = Parameters.EnemyHealth,
    bool IsTriggered = false)
    : GameCharacter(Id, Position, Inventory, Health);
