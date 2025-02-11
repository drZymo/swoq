using Swoq.Interface;

namespace Swoq.Infra;

public enum GameCharacterId
{
    Player1,
    Player2,
    Enemy1,
    Enemy2,
    Enemy3,
}

public abstract record GameCharacter(
    GameCharacterId Id,
    Position Position,
    Inventory Inventory,
    int Health,
    bool HasSword)
{
    public bool IsAlive => Health > 0;
    public bool IsPresent => Position.IsValid;
}

public record Player(
    GameCharacterId Id,
    Position Position,
    Inventory Inventory,
    int Health,
    bool HasSword)
    : GameCharacter(Id, Position, Inventory, Health, HasSword);

public record Enemy(
    GameCharacterId Id,
    Position Position,
    Inventory Inventory,
    int Health,
    bool HasSword,
    int Damage,
    bool IsBoss = false,
    bool IsTriggered = false)
    : GameCharacter(Id, Position, Inventory, Health, HasSword);
