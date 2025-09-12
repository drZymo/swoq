import { Position, Tile } from "./generated/swoq";
import { addPosition, removePosition } from "./position";
import {
    Color,
    isWalkable,
    TILE_CHARS,
    TILE_TO_COLOR,
    tilesFromString,
} from "./tile";

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
    public doorPositions: Partial<Record<Color, Position[]>> = {};
    public doorsUnlocked: Partial<Record<Color, boolean>> = {};
    public pressurePlatePositions: Partial<Record<Color, Position[]>> = {};
    public tilePositions: Partial<Record<Tile, Position[]>> = {};
    public player1Position: Position | undefined;
    public player2Position: Position | undefined;

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
        return isWalkable(this.tiles[pos.y][pos.x]);
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

    private _setTile(
        x: number,
        y: number,
        tile: Tile,
        updatingInternal: boolean = false,
    ): void {
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
            pos,
        );

        // Special handling for unknown tiles: only add/remove boundary nodes in the unknown tile list
        // Most likely: prev=UNKNOWN, tile=EMPTY, but e.g. also prev=DOOR_RED, tile=EMPTY...
        if (!isWalkable(prevTile) && isWalkable(tile)) {
            let tp = this.tilePositions[Tile.UNKNOWN];
            for (const neigh of this.getAllNeighborsOfType(pos, Tile.UNKNOWN)) {
                tp = addPosition(tp, neigh);
            }
            this.tilePositions[Tile.UNKNOWN] = tp;
        }
        this.tilePositions[tile] = addPosition(this.tilePositions[tile], pos);

        // If doors or pressureplates disappear, we can infer what happened to
        // their siblings / targets
        if (!updatingInternal) {
            // TODO: updatingInternal is ugly, but it's to prevent possible recursion
            // because of these updates. This needs to split out to something more clear.
            switch (prevTile) {
                // If one door is removed, all corresponding doors are removed
                // TODO: double-check whether this is true
                case Tile.DOOR_RED:
                case Tile.DOOR_GREEN:
                case Tile.DOOR_BLUE:
                // If pressure plate is pressed, their doors must have disappeared
                case Tile.PRESSURE_PLATE_RED:
                case Tile.PRESSURE_PLATE_GREEN:
                case Tile.PRESSURE_PLATE_BLUE:
                    {
                        const color = TILE_TO_COLOR[prevTile]!;
                        const doors = this.doorPositions[color] ?? [];
                        for (const door of doors) {
                            this._setTile(door.x, door.y, Tile.EMPTY, true);
                        }
                        if (!this.pressurePlatePositions[color]) {
                            // Door with a key, so definitely unlocked
                            this.doorsUnlocked[color] = true;
                        }
                    }
                    break;
            }
        }

        // Remember locations of things that can be occluded or are easier to
        // find like this
        switch (tile) {
            case Tile.EXIT:
                this.exitPosition = { x, y };
                break;
            case Tile.DOOR_RED:
            case Tile.DOOR_GREEN:
            case Tile.DOOR_BLUE:
                {
                    const color = TILE_TO_COLOR[tile]!;
                    this.doorPositions[color] = addPosition(
                        this.doorPositions[color],
                        pos,
                    );
                }
                break;
            case Tile.PRESSURE_PLATE_RED:
            case Tile.PRESSURE_PLATE_GREEN:
            case Tile.PRESSURE_PLATE_BLUE:
                {
                    const color = TILE_TO_COLOR[tile]!;
                    this.pressurePlatePositions[color] = addPosition(
                        this.pressurePlatePositions[color],
                        pos,
                    );
                }
                break;
        }
    }

    public updateSurroundings(
        position: Position,
        surroundings: Tile[],
        visibilityRange: number,
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

    public setPlayerPositions(
        pos1: Position | undefined,
        pos2: Position | undefined,
    ): void {
        // We won't receive further updates, so mark players
        // as empty when they disappeared (i.e. exited).
        if (this.player1Position && !pos1) {
            this.setTile(this.player1Position, Tile.EMPTY);
        }
        if (this.player2Position && !pos2) {
            this.setTile(this.player2Position, Tile.EMPTY);
        }
        this.player1Position = pos1;
        this.player2Position = pos2;
    }

    public toString(): string {
        const rows = this.tiles.map((row) =>
            row.map((tile) => TILE_CHARS[tile] ?? "?"),
        );
        if (this.player1Position) {
            rows[this.player1Position.y][this.player1Position.x] = "1";
        }
        if (this.player2Position) {
            rows[this.player2Position.y][this.player2Position.x] = "2";
        }
        return rows.map((row) => row.join("")).join("\n");
    }

    public getNeighbors(pos: Position): Position[] {
        const neighbors: Position[] = [];
        for (const [dx, dy] of NEIGHBOR_COORDS) {
            const x = pos.x + dx;
            const y = pos.y + dy;
            if (
                isWalkable(this.tiles[y][x]) &&
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
