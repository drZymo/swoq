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
    DoorGreenClosed,
    DoorGreenOpen,
    KeyGreen,
    DoorBlueClosed,
    DoorBlueOpen,
    KeyBlue,

    DoorBlackClosed,
    DoorBlackOpen,
    PressurePlate,

	Sword,
    Health,

    Boulders,
    PressurePlateWithBoulders,
}
