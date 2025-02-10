import { expect, it } from "@jest/globals";
import { Grid } from "./Grid";
import { Tile } from "./generated/swoq";
import { tilesFromString } from "./tile";

it("loads a grid from a string", () => {
    const grid = Grid.fromString(`
        ######
        #    #
        #    #
        #    #
        #    #
        ######
    `);

    expect(grid.width).toBe(6);
    expect(grid.height).toBe(6);
    expect(grid.getTile(0, 0)).toBe(Tile.WALL);
    expect(grid.getTile(2, 0)).toBe(Tile.WALL);
    expect(grid.getTile(1, 1)).toBe(Tile.EMPTY);
    expect(grid.getTile(2, 2)).toBe(Tile.EMPTY);
    expect(grid.getTile(3, 3)).toBe(Tile.EMPTY);
    expect(grid.getTile(4, 4)).toBe(Tile.EMPTY);
    expect(grid.getTile(5, 5)).toBe(Tile.WALL);
});

it("detects boundaries", () => {
    const grid = Grid.fromString(`
        ????
        ????
        ????
        ????
    `);
    expect(grid.tilePositions[Tile.UNKNOWN]).toEqual(undefined);

    const update = tilesFromString(`
        ###
        #1_
        #__
    `);
    console.log("-----------------");
    grid.updateSurroundings({ x: 1, y: 1 }, update.flat(), 1);
    const newGrid = tilesFromString(`
        ###?
        #1_x
        #__x
        ?xx?
    `);
    expect(grid.tiles).toEqual(newGrid);
    expect(grid.tilePositions[Tile.UNKNOWN]).toHaveLength(4);
    expect(grid.tilePositions[Tile.UNKNOWN]).toContainEqual({ x: 3, y: 1 });
    expect(grid.tilePositions[Tile.UNKNOWN]).toContainEqual({ x: 3, y: 2 });
    expect(grid.tilePositions[Tile.UNKNOWN]).toContainEqual({ x: 1, y: 3 });
    expect(grid.tilePositions[Tile.UNKNOWN]).toContainEqual({ x: 2, y: 3 });
});
