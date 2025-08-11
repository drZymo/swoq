namespace Swoq.Infra;

using System.Collections.Immutable;

public class Map(
    int level,
    int height,
    int width,
    IEnumerable<Cell> data,
    bool isFinal,
    Player? player1,
    Player? player2,
    Enemy? enemy1,
    Enemy? enemy2,
    Enemy? enemy3)
{
    public static readonly Map Empty = new(-1, 0, 0, [], false, null, null, null, null, null);

    private readonly IImmutableList<Cell> cells = data.ToImmutableArray();

    public int Level { get; } = level;
    public int Height { get; } = height;
    public int Width { get; } = width;

    public Player? Player1 { get; } = player1;
    public Player? Player2 { get; } = player2;
    public Enemy? Enemy1 { get; } = enemy1;
    public Enemy? Enemy2 { get; } = enemy2;
    public Enemy? Enemy3 { get; } = enemy3;

    public bool IsFinal { get; } = isFinal;

    public Cell this[int y, int x] => cells[y * Width + x];

    public Cell this[Position pos] => cells[pos.index];

    public Cell this[int index] => cells[index];

    public Position Pos(int y, int x) => new(y, x, y * Width + x);

    public Map Set(int y, int x, Cell cell)
    {
        var cells = this.cells.SetItem(y * Width + x, cell);
        return new Map(Level, Height, Width, cells, IsFinal, Player1, Player2, Enemy1, Enemy2, Enemy3);
    }

    public Map Set(Position pos, Cell cell) => Set(pos.y, pos.x, cell);

    public Map SetCharacter(GameCharacter newCharacter)
    {
        var player1 = Player1;
        var player2 = Player2;
        var enemy1 = Enemy1;
        var enemy2 = Enemy2;
        var enemy3 = Enemy3;
        switch (newCharacter.Id)
        {
            case GameCharacterId.Player1:
                if (newCharacter is Player p1)
                {
                    player1 = p1;
                }
                break;
            case GameCharacterId.Player2:
                if (newCharacter is Player p2)
                {
                    player2 = p2;
                }
                break;
            case GameCharacterId.Enemy1:
                if (newCharacter is Enemy e1)
                {
                    enemy1 = e1;
                }
                break;
            case GameCharacterId.Enemy2:
                if (newCharacter is Enemy e2)
                {
                    enemy2 = e2;
                }
                break;
            case GameCharacterId.Enemy3:
                if (newCharacter is Enemy e3)
                {
                    enemy3 = e3;
                }
                break;
        }
        return new Map(Level, Height, Width, cells, IsFinal, player1, player2, enemy1, enemy2, enemy3);
    }

    public IEnumerable<GameCharacter> AllCharacters
    {
        get
        {
            if (Player1 != null) yield return Player1;
            if (Player2 != null) yield return Player2;
            if (Enemy1 != null) yield return Enemy1;
            if (Enemy2 != null) yield return Enemy2;
            if (Enemy3 != null) yield return Enemy3;
        }
    }

    public IEnumerable<Enemy> Enemies
    {
        get
        {
            if (Enemy1 != null) yield return Enemy1;
            if (Enemy2 != null) yield return Enemy2;
            if (Enemy3 != null) yield return Enemy3;
        }
    }
}
