import { Position } from "./generated/swoq";
import { Grid } from "./Grid";

export class Dijkstra {
    private grid: Grid;
    private from: Position;
    private distances: Map<number, number> = new Map();
    private previous: Map<number, Position> = new Map();

    constructor(grid: Grid, from: Position) {
        this.grid = grid;
        this.from = from;
        this.distances = new Map();
        this.previous = new Map();
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

            const neighbors = this.grid.getNeighbors(currentPosition);
            for (const neighbor of neighbors) {
                const neighborKey = neighbor.y * width + neighbor.x;
                const distance = currentDistance + 1; //this.grid.getCost(currentPosition, neighbor);

                if (
                    !this.distances.has(neighborKey) ||
                    distance < this.distances.get(neighborKey)!
                ) {
                    this.distances.set(neighborKey, distance);
                    this.previous.set(neighborKey, currentPosition);
                    pq.push([distance, neighbor]);
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
        let currentKey = to.y * this.grid.width + to.x;

        while (this.previous.has(currentKey)) {
            const currentPosition = this.previous.get(currentKey)!;
            path.unshift(currentPosition);
            currentKey =
                currentPosition.y * this.grid.width + currentPosition.x;
        }

        if (path.length === 0 || path[0] !== this.from) {
            path.unshift(this.from);
        }

        return path.slice(1);
    }
}
