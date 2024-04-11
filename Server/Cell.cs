namespace Swoq.Server;

enum CellType
{
    Empty,
    Wall,
    Exit,
}

record Cell(CellType Type, bool IsVisible);
