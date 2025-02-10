import { Position, Tile } from "./generated/swoq";
import { addPosition, removePosition } from "./position";
import { TILE_CHARS, tilesFromString } from "./tile";

const NEIGHBOR_COORDS: [number, number][] = [
    [-1, 0],
    [1, 0],
    [0, -1],
    [0, 1],
];

export class Grid {
    public readonly width: number;
    public readonly height: number;
    public readonly tiles: Tile[][];
    public exitPosition?: Position;
    // public doorPositions: Partial<Record<Color, Position[]>> = {};
    public tilePositions: Partial<Record<Tile, Position[]>> = {};

    public static fromString(str: string): Grid {
        const tiles = tilesFromString(str);
        const height = tiles.length;
        const width = tiles[0].length;
        const grid = new Grid(width, height);
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                const tile = tiles[y][x];
                if (tile !== Tile.UNKNOWN) {
                    grid.setTile(x, y, tile);
                }
            }
        }
        return grid;
    }

    public static isWalkable(tile: Tile): boolean {
        switch (tile) {
            case Tile.EMPTY:
            case Tile.EXIT:
                return true;
            case Tile.PLAYER:
            case Tile.UNKNOWN:
            case Tile.WALL:
            case Tile.DOOR_RED:
            case Tile.KEY_RED:
            case Tile.DOOR_GREEN:
            case Tile.KEY_GREEN:
            case Tile.DOOR_BLUE:
            case Tile.KEY_BLUE:
            case Tile.BOULDER:
                return false;
        }
    }

    constructor(width: number, height: number) {
        this.width = width;
        this.height = height;
        this.tiles = new Array(height);
        for (let y = 0; y < height; y++) {
            this.tiles[y] = new Array(width);
            for (let x = 0; x < width; x++) {
                this.tiles[y][x] = Tile.UNKNOWN;
            }
        }
    }

    public getTile(x: number, y: number): Tile;
    public getTile(pos: Position): Tile;
    public getTile(...args: [number, number] | [Position]): Tile {
        if (args.length === 1) {
            const [pos] = args;
            return this.tiles[pos.y][pos.x];
        } else {
            const [x, y] = args;
            return this.tiles[y][x];
        }
    }

    public isWalkable(pos: Position): boolean {
        return Grid.isWalkable(this.tiles[pos.y][pos.x]);
    }

    public setTile(x: number, y: number, tile: Tile): void;
    public setTile(pos: Position, tile: Tile): void;
    public setTile(...args: [number, number, Tile] | [Position, Tile]): void {
        if (args.length === 2) {
            const [pos, tile] = args;
            this._setTile(pos.x, pos.y, tile);
        } else {
            this._setTile(...args);
        }
    }

    private _setTile(x: number, y: number, tile: Tile): void {
        if (x < 0 || x >= this.width || y < 0 || y >= this.height) {
            return;
        }
        const prevTile = this.tiles[y][x];
        if (prevTile === tile) {
            return;
        }

        this.tiles[y][x] = tile;
        const pos = { x, y };

        if (tile === Tile.UNKNOWN) {
            throw new Error(`assertion failed: tile cannot be set to UNKNOWN`);
        }
        this.tilePositions[prevTile] = removePosition(
            this.tilePositions[prevTile],
            pos
        );
        if (prevTile === Tile.UNKNOWN && tile === Tile.EMPTY) {
            // Special handling for unknown tiles: only add/remove boundary nodes in the unknown tile list
            let tp = this.tilePositions[Tile.UNKNOWN];
            for (const neigh of this.getAllNeighborsOfType(pos, Tile.UNKNOWN)) {
                tp = addPosition(tp, neigh);
            }
            this.tilePositions[Tile.UNKNOWN] = tp;
        }
        this.tilePositions[tile] = addPosition(this.tilePositions[tile], pos);
        switch (tile) {
            case Tile.EXIT:
                this.exitPosition = { x, y };
                break;
        }
    }

    public updateSurroundings(
        position: Position,
        surroundings: Tile[],
        visibilityRange: number
    ): void {
        const x = position.x;
        const y = position.y;
        let si = 0;
        for (let dy = -visibilityRange; dy <= visibilityRange; dy++) {
            const row = y + dy;
            for (let dx = -visibilityRange; dx <= visibilityRange; dx++) {
                const col = x + dx;
                const tile = surroundings[si++];
                if (tile !== Tile.UNKNOWN) {
                    this._setTile(col, row, tile);
                }
            }
        }
    }

    public toString(): string {
        return this.tiles
            .map((row) => row.map((tile) => TILE_CHARS[tile] ?? "?").join(""))
            .join("\n");
    }

    public getNeighbors(pos: Position): Position[] {
        const neighbors: Position[] = [];
        for (const [dx, dy] of NEIGHBOR_COORDS) {
            const x = pos.x + dx;
            const y = pos.y + dy;
            if (
                Grid.isWalkable(this.tiles[y][x]) &&
                x >= 0 &&
                x < this.width &&
                y >= 0 &&
                y < this.height
            ) {
                neighbors.push({ x, y });
            }
        }
        return neighbors;
    }

    public getAllNeighborsOfType(pos: Position, type: Tile): Position[] {
        const neighbors: Position[] = [];
        for (const [dx, dy] of NEIGHBOR_COORDS) {
            const x = pos.x + dx;
            const y = pos.y + dy;
            if (
                x >= 0 &&
                x < this.width &&
                y >= 0 &&
                y < this.height &&
                this.tiles[y][x] === type
            ) {
                neighbors.push({ x, y });
            }
        }
        return neighbors;
    }

    public getAllNeighbors(pos: Position): Position[] {
        const neighbors: Position[] = [];
        for (const [dx, dy] of NEIGHBOR_COORDS) {
            const x = pos.x + dx;
            const y = pos.y + dy;
            if (x >= 0 && x < this.width && y >= 0 && y < this.height) {
                neighbors.push({ x, y });
            }
        }
        return neighbors;
    }

    // public getUnknownPositions(): Position[] {
    //     const unknowns: Position[] = [];
    //     for (let y = 0; y < this.height; y++) {
    //         for (let x = 0; x < this.width; x++) {
    //             if (this.tiles[y][x] === Tile.UNKNOWN) {
    //                 unknowns.push({ x, y });
    //             }
    //         }
    //     }
    //     return unknowns;
    // }
}
