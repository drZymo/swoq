using Swoq.Interface;

namespace Swoq.Server;

using Position = (int y, int x);

internal enum GameCharacterId
{
    Player1,
    Player2,
    Enemy1,
    Enemy2,
    Enemy3,
    Boss,
}

internal abstract record GameCharacter(
    GameCharacterId Id,
    Position Position,
    Inventory Inventory,
    int Health,
    bool HasSword);

internal record Player(
    GameCharacterId Id,
    Position Position,
    Inventory Inventory = Inventory.None,
    int Health = Parameters.PlayerHealth,
    bool HasSword = false)
    : GameCharacter(Id, Position, Inventory, Health, HasSword);

internal record Enemy(
    GameCharacterId Id,
    Position Position,
    Inventory Inventory = Inventory.None,
    int Health = Parameters.EnemyHealth,
    bool HasSword = true,
    int Damage = Parameters.EnemyDamage,
    bool IsTriggered = false)
    : GameCharacter(Id, Position, Inventory, Health, HasSword);
