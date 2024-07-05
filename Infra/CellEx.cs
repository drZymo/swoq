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
            case Cell.PressurePlateRed:
            case Cell.DoorGreenOpen:
            case Cell.KeyGreen:
            case Cell.PressurePlateGreen:
            case Cell.DoorBlueOpen:
            case Cell.KeyBlue:
            case Cell.PressurePlateBlue:
            case Cell.Sword:
            case Cell.Health:
                return true;

            case Cell.Wall:
            case Cell.DoorRedClosed:
            case Cell.DoorGreenClosed:
            case Cell.DoorBlueClosed:
            case Cell.Boulder:
            case Cell.PressurePlateRedWithBoulder:
            case Cell.PressurePlateGreenWithBoulder:
            case Cell.PressurePlateBlueWithBoulder:
                return false;
        };
        return false;
    }
}
