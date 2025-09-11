namespace Swoq.Infra;

public static class CellExtensions
{
    private static readonly HashSet<Cell> WalkableCells = [
        Cell.Empty,
        Cell.Exit,
        Cell.DoorRedOpen,
        Cell.DoorGreenOpen,
        Cell.DoorBlueOpen,
        Cell.KeyRed,
        Cell.KeyGreen,
        Cell.KeyBlue,
        Cell.PressurePlateRed,
        Cell.PressurePlateGreen,
        Cell.PressurePlateBlue,
        Cell.Sword,
        Cell.Health,
        Cell.Treasure,
    ];

    private static readonly HashSet<Cell> EmptyCells = [
        Cell.Empty,
        Cell.DoorRedOpen,
        Cell.DoorGreenOpen,
        Cell.DoorBlueOpen,
    ];

    public static bool CanWalkOn(this Cell cell) => WalkableCells.Contains(cell);

    public static bool IsEmpty(this Cell cell) => EmptyCells.Contains(cell);
}
