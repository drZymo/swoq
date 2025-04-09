import { expect, it } from "@jest/globals";
import { Dijkstra } from "./Dijkstra";
import { Grid } from "./Grid";

it("should find the shortest path in empty space", () => {
    const grid = Grid.fromString(`
        ######
        #@   #
        ######
    `);
    const d = new Dijkstra(grid, { x: 1, y: 1 });
    expect(d.getPath({ x: 3, y: 1 })).toEqual([
        { x: 1, y: 1 },
        { x: 2, y: 1 },
        { x: 3, y: 1 },
    ]);
});

it("should return 2-element path when next to it", () => {
    const grid = Grid.fromString(`
        ######
        #@   #
        ######
    `);
    const d = new Dijkstra(grid, { x: 1, y: 1 });
    expect(d.getPath({ x: 2, y: 1 })).toEqual([
        { x: 1, y: 1 },
        { x: 2, y: 1 },
    ]);
});

it("should find the shortest path to non-walkable location", () => {
    const grid = Grid.fromString(`
        ######
        #@ # #
        ######
    `);
    const d = new Dijkstra(grid, { x: 1, y: 1 });
    expect(d.getPath({ x: 3, y: 1 })).toEqual([
        { x: 1, y: 1 },
        { x: 2, y: 1 },
        { x: 3, y: 1 },
    ]);
});

it("should not find a path to unreachable", () => {
    const grid = Grid.fromString(`
        ######
        #@ # #
        ######
    `);
    const d = new Dijkstra(grid, { x: 1, y: 1 });
    expect(d.getPath({ x: 4, y: 1 })).toEqual([]);
});
