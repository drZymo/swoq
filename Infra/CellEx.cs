namespace Swoq.Infra;

public static class CellEx
{
    public static bool CanWalkOn(this Cell cell)
    {
        switch (cell)
        {
            case Cell.Empty:
            case Cell.Exit:
            case Cell.DoorRedOpen:
            case Cell.KeyRed:
            case Cell.DoorGreenOpen:
            case Cell.KeyGreen:
            case Cell.DoorBlueOpen:
            case Cell.KeyBlue:
            case Cell.DoorBlackOpen:
            case Cell.PressurePlate:
            case Cell.Sword:
                return true;

            case Cell.Wall:
            case Cell.DoorRedClosed:
            case Cell.DoorGreenClosed:
            case Cell.DoorBlueClosed:
            case Cell.DoorBlackClosed:
                return false;
        };
        return false;
    }
}
