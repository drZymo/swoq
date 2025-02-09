import { Position, Tile } from "./generated/swoq";

const TILE_CHARS: Record<Tile, string> = {
    [Tile.UNKNOWN]: ".",
    [Tile.WALL]: "#",
    [Tile.EMPTY]: " ",
    [Tile.PLAYER]: "1",
    [Tile.EXIT]: "$",
};

export class Grid {
    public readonly width: number;
    public readonly height: number;
    public readonly tiles: Tile[][];
    public exitPosition?: Position;

    public static isWalkable(tile: Tile): boolean {
        switch (tile) {
            case Tile.EMPTY:
            case Tile.EXIT:
            case Tile.PLAYER: // otherwise AStar won't find a path
            case Tile.UNKNOWN:
                return true;
            case Tile.WALL:
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
        this.tiles[y][x] = tile;
        if (tile === Tile.EXIT) {
            this.exitPosition = { x, y };
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
        for (const [dx, dy] of [
            [-1, 0],
            [1, 0],
            [0, -1],
            [0, 1],
        ]) {
            const x = pos.x + dx;
            const y = pos.y + dy;
            if (
                x >= 0 &&
                x < this.width &&
                y >= 0 &&
                y < this.height &&
                Grid.isWalkable(this.tiles[y][x])
            ) {
                neighbors.push({ x, y });
            }
        }
        return neighbors;
    }

    public getUnknownPositions(): Position[] {
        const unknowns: Position[] = [];
        for (let y = 0; y < this.height; y++) {
            for (let x = 0; x < this.width; x++) {
                if (this.tiles[y][x] === Tile.UNKNOWN) {
                    unknowns.push({ x, y });
                }
            }
        }
        return unknowns;
    }
}
