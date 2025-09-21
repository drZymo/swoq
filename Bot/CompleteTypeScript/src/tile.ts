import { Tile } from "./generated/swoq";

export const TILE_CHARS: Record<Tile, string> = {
    [Tile.UNKNOWN]: ".",
    [Tile.WALL]: "#",
    [Tile.EMPTY]: " ",
    [Tile.PLAYER]: "@",
    [Tile.EXIT]: "*",
    [Tile.DOOR_RED]: "R",
    [Tile.KEY_RED]: "r",
    [Tile.DOOR_GREEN]: "G",
    [Tile.KEY_GREEN]: "g",
    [Tile.DOOR_BLUE]: "B",
    [Tile.KEY_BLUE]: "b",
    [Tile.BOULDER]: "O",
    [Tile.PRESSURE_PLATE_RED]: "u",
    [Tile.PRESSURE_PLATE_GREEN]: "v",
    [Tile.PRESSURE_PLATE_BLUE]: "w",
    [Tile.ENEMY]: "E",
    [Tile.HEALTH]: "%",
    [Tile.SWORD]: "/",
};

export const CHAR_TO_TILE: Record<string, Tile> = {
    ".": Tile.UNKNOWN,
    "#": Tile.WALL,
    " ": Tile.EMPTY,
    _: Tile.EMPTY, // for use in tests
    "1": Tile.PLAYER,
    $: Tile.EXIT,
    R: Tile.DOOR_RED,
    r: Tile.KEY_RED,
    G: Tile.DOOR_GREEN,
    g: Tile.KEY_GREEN,
    B: Tile.DOOR_BLUE,
    b: Tile.KEY_BLUE,
    O: Tile.BOULDER,
    u: Tile.PRESSURE_PLATE_RED,
    v: Tile.PRESSURE_PLATE_GREEN,
    w: Tile.PRESSURE_PLATE_BLUE,
    E: Tile.ENEMY,
};

export enum Color {
    Red,
    Green,
    Blue,
}

export const COLORS = [Color.Red, Color.Green, Color.Blue];

export const COLOR_TO_DOOR_TILE: Record<Color, Tile> = {
    [Color.Red]: Tile.DOOR_RED,
    [Color.Green]: Tile.DOOR_GREEN,
    [Color.Blue]: Tile.DOOR_BLUE,
};

export const COLOR_TO_KEY_TILE: Record<Color, Tile> = {
    [Color.Red]: Tile.KEY_RED,
    [Color.Green]: Tile.KEY_GREEN,
    [Color.Blue]: Tile.KEY_BLUE,
};

export const COLOR_TO_PLATE_TILE: Record<Color, Tile> = {
    [Color.Red]: Tile.PRESSURE_PLATE_RED,
    [Color.Green]: Tile.PRESSURE_PLATE_GREEN,
    [Color.Blue]: Tile.PRESSURE_PLATE_BLUE,
};

export const TILE_TO_COLOR: Record<Tile, Color | undefined> = {
    [Tile.DOOR_RED]: Color.Red,
    [Tile.KEY_RED]: Color.Red,
    [Tile.PRESSURE_PLATE_RED]: Color.Red,
    [Tile.DOOR_GREEN]: Color.Green,
    [Tile.KEY_GREEN]: Color.Green,
    [Tile.PRESSURE_PLATE_GREEN]: Color.Green,
    [Tile.DOOR_BLUE]: Color.Blue,
    [Tile.KEY_BLUE]: Color.Blue,
    [Tile.PRESSURE_PLATE_BLUE]: Color.Blue,

    [Tile.UNKNOWN]: undefined,
    [Tile.EMPTY]: undefined,
    [Tile.PLAYER]: undefined,
    [Tile.WALL]: undefined,
    [Tile.EXIT]: undefined,
    [Tile.BOULDER]: undefined,
    [Tile.ENEMY]: undefined,
    [Tile.SWORD]: undefined,
    [Tile.HEALTH]: undefined,
};

export function isWalkable(tile: Tile): boolean {
    switch (tile) {
        case Tile.EMPTY:
        case Tile.PRESSURE_PLATE_RED:
        case Tile.PRESSURE_PLATE_GREEN:
        case Tile.PRESSURE_PLATE_BLUE:
            return true;
        // Definitely unwalkable
        case Tile.WALL:
        case Tile.BOULDER:
        case Tile.UNKNOWN:
        case Tile.DOOR_RED:
        case Tile.DOOR_GREEN:
        case Tile.DOOR_BLUE:
        // Not walkable to make sure we navigate around them
        case Tile.PLAYER:
        case Tile.ENEMY:
        // Not walkable to prevent accidental pickup (which may cause
        // us getting stuck, or receive an inventory-full error)
        case Tile.HEALTH:
        case Tile.SWORD:
        case Tile.KEY_RED:
        case Tile.KEY_GREEN:
        case Tile.KEY_BLUE:
        // Not walkable to prevent accidentally walking
        // into it with a boulder, or before we freed
        // the other player.
        case Tile.EXIT:
            return false;
    }
}

export function tilesFromString(str: string): Tile[][] {
    const rows = str.trim().split("\n");
    const height = rows.length;
    const width = rows[0].length;
    const matrix: Tile[][] = [];
    for (let y = 0; y < height; y++) {
        const row = rows[y].trim();
        matrix[y] = Array(width);
        for (let x = 0; x < width; x++) {
            const char = row[x];
            const tile = CHAR_TO_TILE[char] ?? Tile.UNKNOWN;
            matrix[y][x] = tile;
        }
    }
    return matrix;
}
