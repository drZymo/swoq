namespace Swoq.Infra;

using Swoq.Interface;
using System.Collections.Immutable;

public class MutableCharacter
{
    public Position Position { get; set; } = Position.Invalid;
    public Inventory Inventory { get; set; } = Inventory.None;
    public bool IsBoss { get; set; } = false;

    // TODO: Get from Parameters
    private const int PlayerHealth = 5;
    private const int EnemyHealth = 6;
    private const int EnemyDamage = 1;
    private const int BossHealth = 100;
    private const int BossDamage = 100;

    public Player ToPlayer(GameCharacterId id)
    {
        return new Player(id, Position, Inventory, PlayerHealth, false);
    }
    public Enemy ToEnemy(GameCharacterId id, bool isTwoPlayerGame)
    {
        return new Enemy(
            id,
            Position,
            Inventory,
            IsBoss ? BossHealth : EnemyHealth,
            !IsBoss && isTwoPlayerGame,
            IsBoss ? BossDamage : EnemyDamage,
            IsBoss);
    }
}

public class MutableMap(int level, int height, int width)
{
    private readonly Cell[] data = new Cell[height * width];

    public int Level { get; } = level;
    public int Height { get; } = height;
    public int Width { get; } = width;

    public MutableCharacter Player1 { get; } = new();
    public MutableCharacter Player2 { get; } = new();
    public MutableCharacter Enemy1 { get; } = new();
    public MutableCharacter Enemy2 { get; } = new();
    public MutableCharacter Enemy3 { get; } = new();

    public bool IsFinal { get; set; } = false;

    public Cell this[int y, int x]
    {
        get => data[y * Width + x];
        set => data[y * Width + x] = value;
    }

    public Cell this[Position pos]
    {
        get => data[pos.index];
        set => data[pos.index] = value;
    }

    public Cell this[int index]
    {
        get => data[index];
        set => data[index] = value;
    }

    public Position Pos(int y, int x) => new(y, x, y * Width + x);

    public Map ToMap()
    {
        Player? player1 = null;
        Player? player2 = null;
        ImmutableDictionary<GameCharacterId, Enemy> enemies = ImmutableDictionary<GameCharacterId, Enemy>.Empty;

        if (Player1.Position.IsValid)
        {
            player1 = Player1.ToPlayer(GameCharacterId.Player1);
        }
        if (Player2.Position.IsValid)
        {
            player2 = Player2.ToPlayer(GameCharacterId.Player2);
        }
        var isTwoPlayerGame = player1 != null && player2 != null;

        var nextEnemyId = GameCharacterId.Enemy1;
        if (Enemy1.Position.IsValid)
        {
            var enemy = Enemy1.ToEnemy(nextEnemyId, isTwoPlayerGame);
            enemies = enemies.SetItem(enemy.Id, enemy);
            nextEnemyId++;
        }
        if (Enemy2.Position.IsValid)
        {
            var enemy = Enemy2.ToEnemy(nextEnemyId, isTwoPlayerGame);
            enemies = enemies.SetItem(enemy.Id, enemy);
            nextEnemyId++;
        }
        if (Enemy3.Position.IsValid)
        {
            var enemy = Enemy3.ToEnemy(nextEnemyId, isTwoPlayerGame);
            enemies = enemies.SetItem(enemy.Id, enemy);
        }

        return new Map(Level, Height, Width, data, IsFinal, player1, player2,
            enemies.TryGetValue(GameCharacterId.Enemy1, out var e1) ? e1 : null,
            enemies.TryGetValue(GameCharacterId.Enemy2, out var e2) ? e2 : null,
            enemies.TryGetValue(GameCharacterId.Enemy3, out var e3) ? e3 : null);
    }
}
