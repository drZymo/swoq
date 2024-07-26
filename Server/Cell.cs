namespace Swoq.Server;

enum CellType
{
    Empty,
    Wall,
    Exit,

    DoorRed,
    KeyRed,
}

record Cell(CellType Type, bool IsVisible);
