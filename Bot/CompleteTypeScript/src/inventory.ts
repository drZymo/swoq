import { Inventory } from "./generated/swoq";
import { Color } from "./tile";

export const COLOR_TO_KEY_INVENTORY: Record<Color, Inventory> = {
    [Color.Red]: Inventory.KEY_RED,
    [Color.Green]: Inventory.KEY_GREEN,
    [Color.Blue]: Inventory.KEY_BLUE,
};
