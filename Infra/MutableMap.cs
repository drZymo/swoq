namespace Swoq.Infra;

using Swoq.Interface;
using Position = (int y, int x);

public class MutableCharacter
{
    public Position Position { get; set; } = PositionEx.Invalid;
    public Inventory Inventory { get; set; } = Inventory.None;
    public bool IsBoss { get; set; } = false;
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
        get => data[pos.y * Width + pos.x];
        set => data[pos.y * Width + pos.x] = value;
    }

    public Map ToMap()
    {
        return new Map(
            Level,
            data, Height, Width,
            Player1.Position,
            Player2.Position.IsValid() ? Player2.Position : null,
            Enemy1.Position.IsValid() ? Enemy1.Position : null,
            Enemy1.Position.IsValid() ? Enemy1.Inventory : Inventory.None,
            Enemy1.IsBoss,
            Enemy2.Position.IsValid() ? Enemy2.Position : null,
            Enemy2.Position.IsValid() ? Enemy2.Inventory : Inventory.None,
            Enemy3.Position.IsValid() ? Enemy3.Position : null,
            Enemy3.Position.IsValid() ? Enemy3.Inventory : Inventory.None,
            isFinal: IsFinal);
    }
}
