namespace Swoq.Infra;

public static class Converters
{
    public static Inventory ToInventory(this Cell cell) => cell switch
    {
        Cell.KeyRed => Inventory.KeyRed,
        Cell.KeyGreen => Inventory.KeyGreen,
        Cell.KeyBlue => Inventory.KeyBlue,
        Cell.Boulders => Inventory.Boulders,
        _ => Inventory.None,
    };

    public static Cell ToDroppedLoot(this Inventory inventory) => inventory switch
    {
        Inventory.None => Cell.Empty,
        Inventory.KeyRed => Cell.KeyRed,
        Inventory.KeyGreen => Cell.KeyGreen,
        Inventory.KeyBlue => Cell.KeyBlue,
        Inventory.Boulders => Cell.Boulders,
        _ => throw new NotImplementedException(),
    };

    public static Cell ToOpenDoor(this Cell closedDoor) => closedDoor switch
    {
        Cell.DoorRedClosed => Cell.DoorRedOpen,
        Cell.DoorGreenClosed => Cell.DoorGreenOpen,
        Cell.DoorBlueClosed => Cell.DoorBlueOpen,
        Cell.DoorBlackClosed => Cell.DoorBlackOpen,
        _ => throw new NotImplementedException("Not a closed door"),
    };

    public static Cell ToClosedDoor(this Cell openDoor) => openDoor switch
    {
        Cell.DoorRedOpen => Cell.DoorRedClosed,
        Cell.DoorGreenOpen => Cell.DoorGreenClosed,
        Cell.DoorBlueOpen => Cell.DoorBlueClosed,
        Cell.DoorBlackOpen => Cell.DoorBlackClosed,
        _ => throw new NotImplementedException("Not an open door"),
    };
}
