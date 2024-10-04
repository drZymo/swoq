namespace Swoq.Infra;

using System.Collections.Immutable;
using Position = (int y, int x);

public class Map(
    int level,
    int height,
    int width,
    IEnumerable<Cell> data,
    bool isFinal,
    Player? player1,
    Player? player2,
    ImmutableDictionary<GameCharacterId, Enemy> enemies)
{
    public static readonly Map Empty = new(-1, 0, 0, [], false, null, null, ImmutableDictionary<GameCharacterId, Enemy>.Empty);

    private readonly IImmutableList<Cell> cells = data.ToImmutableArray();

    public int Level { get; } = level;
    public int Height { get; } = height;
    public int Width { get; } = width;

    public Player? Player1 { get; } = player1;
    public Player? Player2 { get; } = player2;
    public ImmutableDictionary<GameCharacterId, Enemy> Enemies { get; } = enemies;

    public bool IsFinal { get; } = isFinal;

    public Cell this[int y, int x] => cells[y * Width + x];

    public Cell this[Position pos] => this[pos.y, pos.x];

    public Map Set(int y, int x, Cell cell)
    {
        var cells = this.cells.SetItem(y * Width + x, cell);
        return new Map(Level, Height, Width, cells, IsFinal, Player1, Player2, Enemies);
    }

    public Map Set(Position pos, Cell cell) => Set(pos.y, pos.x, cell);

    public Map SetCharacter(GameCharacter newCharacter)
    {
        var player1 = Player1;
        var player2 = Player2;
        var enemies = Enemies;
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
            case GameCharacterId.Enemy2:
            case GameCharacterId.Enemy3:
                if (newCharacter is Enemy e)
                {
                    enemies = enemies.SetItem(newCharacter.Id, e);
                }
                break;
        }
        return new Map(Level, Height, Width, cells, IsFinal, player1, player2, enemies);
    }

    public IEnumerable<GameCharacter> AllCharacters
    {
        get
        {
            if (Player1 != null) yield return Player1;
            if (Player2 != null) yield return Player2;
            foreach (var enemy in Enemies.Values) yield return enemy;
        }
    }
}
