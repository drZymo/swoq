import { Position } from "./generated/swoq";
import { Grid } from "./Grid";

export class Dijkstra {
    private grid: Grid;
    private from: Position;
    private distances: Map<number, number> = new Map();
    private previous: Map<number, Position> = new Map();
    private isWalkable: (pos: Position) => boolean;

    constructor(
        grid: Grid,
        from: Position,
        isWalkable: (pos: Position) => boolean = (pos) =>
            this.grid.isWalkable(pos)
    ) {
        this.grid = grid;
        this.from = from;
        this.distances = new Map();
        this.previous = new Map();
        this.isWalkable = isWalkable;
        this.buildDistanceMatrix();
    }

    private buildDistanceMatrix() {
        const width = this.grid.width;
        // const height = this.grid.height;
        const startKey = this.from.y * width + this.from.x;
        const pq: [number, Position][] = [];
        pq.push([0, this.from]);
        this.distances.set(startKey, 0);

        while (pq.length > 0) {
            pq.sort((a, b) => a[0] - b[0]); // TODO needed?
            const [currentDistance, currentPosition] = pq.shift()!;
            // const currentKey = currentPosition.y * width + currentPosition.x;

            const neighbors = this.grid.getAllNeighbors(currentPosition);
            for (const neighbor of neighbors) {
                const neighborKey = neighbor.y * width + neighbor.x;
                // TODO larger weight for enemies?
                const distance = currentDistance + 1; //this.grid.getCost(currentPosition, neighbor);

                if (
                    !this.distances.has(neighborKey) ||
                    distance < this.distances.get(neighborKey)!
                ) {
                    this.distances.set(neighborKey, distance);
                    this.previous.set(neighborKey, currentPosition);
                    if (this.isWalkable(neighbor)) {
                        pq.push([distance, neighbor]);
                    }
                }
            }
        }
    }

    getDistance(to: Position): number | undefined {
        const key = to.y * this.grid.width + to.x;
        return this.distances.get(key);
    }

    getPath(to: Position): Position[] {
        const path: Position[] = [];

        let currentPosition: Position | undefined = to;
        let currentKey = to.y * this.grid.width + to.x;
        while ((currentPosition = this.previous.get(currentKey))) {
            currentKey =
                currentPosition.y * this.grid.width + currentPosition.x;
            path.unshift(currentPosition);
        }

        if (path.length > 0) {
            path.push(to);
        }

        return path;
    }

    public getRandomReachablePosition(): Position | undefined {
        const keys = [...this.distances.keys()];
        while (keys.length > 0) {
            const key = keys.splice(
                Math.floor(Math.random() * keys.length),
                1
            )[0];
            const pos = this._keyToPos(key);
            if (this.isWalkable(pos)) {
                return pos;
            }
        }
        return undefined;
    }

    public dump(): void {
        console.log(
            [...this.distances.entries()].map(([key, distance]) => {
                const pos = this._keyToPos(key);
                const prev = this.previous.get(key) ?? { x: -1, y: -1 };
                return `[${pos.x},${pos.y}] = -> [${prev.x},${prev.y}] @ ${distance}`;
            })
        );
    }

    private _keyToPos(key: number): Position {
        const x = key % this.grid.width;
        const y = Math.floor(key / this.grid.width);
        return { x, y };
    }
}
