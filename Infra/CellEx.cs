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
            case Cell.DoorGreenOpen:
            case Cell.DoorBlueOpen:
            case Cell.KeyRed:
            case Cell.KeyGreen:
            case Cell.KeyBlue:
            case Cell.PressurePlateRed:
            case Cell.PressurePlateGreen:
            case Cell.PressurePlateBlue:
            case Cell.Sword:
            case Cell.Health:
            case Cell.Treasure:
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
        }
        ;
        return false;
    }

    public static bool IsEmpty(this Cell cell)
    {
        switch (cell)
        {
            case Cell.Empty:
            case Cell.DoorRedOpen:
            case Cell.DoorGreenOpen:
            case Cell.DoorBlueOpen:
                return true;

            case Cell.Exit:
            case Cell.KeyRed:
            case Cell.KeyGreen:
            case Cell.KeyBlue:
            case Cell.PressurePlateRed:
            case Cell.PressurePlateGreen:
            case Cell.PressurePlateBlue:
            case Cell.Sword:
            case Cell.Health:
            case Cell.Treasure:
            case Cell.Wall:
            case Cell.DoorRedClosed:
            case Cell.DoorGreenClosed:
            case Cell.DoorBlueClosed:
            case Cell.Boulder:
            case Cell.PressurePlateRedWithBoulder:
            case Cell.PressurePlateGreenWithBoulder:
            case Cell.PressurePlateBlueWithBoulder:
                return false;
        }
        ;
        return false;
    }
}
