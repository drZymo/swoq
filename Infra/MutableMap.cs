namespace Swoq.Infra;

using Position = (int y, int x);

internal class MutableCharacter
{
    public Position Position { get; set; } = PositionEx.Invalid;
    public Inventory Inventory { get; set; } = Inventory.None;
}

internal class MutableMap(int height, int width)
{
    private readonly Cell[] data = new Cell[height * width];

    public int Height { get; } = height;
    public int Width { get; } = width;

    public MutableCharacter Player1 { get; } = new();
    public MutableCharacter Player2 { get; } = new();
    public MutableCharacter Enemy1 { get; } = new();
    public MutableCharacter Enemy2 { get; } = new();

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
            data, Height, Width,
            Player1.Position,
            Player2.Position.IsValid() ? Player2.Position : null,
            Enemy1.Position.IsValid() ? Enemy1.Position : null,
            Enemy1.Position.IsValid() ? Enemy1.Inventory : Inventory.None,
            Enemy2.Position.IsValid() ? Enemy2.Position : null,
            Enemy2.Position.IsValid() ? Enemy2.Inventory : Inventory.None);
    }
}
