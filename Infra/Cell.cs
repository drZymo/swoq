namespace Swoq.Infra;

public enum Cell
{
    Unknown,

    Empty,
    Wall,
    Exit,

    DoorRedClosed,
    DoorRedOpen,
    KeyRed,
    PressurePlateRed,

    DoorGreenClosed,
    DoorGreenOpen,
    KeyGreen,
    PressurePlateGreen,

    DoorBlueClosed,
    DoorBlueOpen,
    KeyBlue,
    PressurePlateBlue,

    Sword,
    Health,

    Boulder,
    PressurePlateRedWithBoulder,
    PressurePlateGreenWithBoulder,
    PressurePlateBlueWithBoulder,
}
