import { AStarFinder } from "astar-typescript";
import { Dijkstra } from "./Dijkstra";
import { Game } from "./Game";
import { DirectedAction, GameStatus, Position } from "./generated/swoq";
import { Grid } from "./Grid";

export class AI {
    public game: Game;

    private _moveEast: boolean = true;
    private _grid: Grid;
    private _aStarInstance: AStarFinder | undefined;

    public constructor(game: Game) {
        this.game = game;
        this._grid = new Grid(game.width, game.height);
    }

    public async play(): Promise<void> {
        let state = this.game.state;
        performance.mark("start");
        console.log(
            `Start state: tick=${state.tick}, level=${state.level}, status=${
                GameStatus[state.status]
            }`
        );

        const level = state.level;
        while (
            this.game.state.status == GameStatus.ACTIVE &&
            level === this.game.state.level
        ) {
            this._updateState();
            await this._step();
        }

        performance.mark("end");
        const playMeasurement = performance.measure("play", "start", "end");
        console.log(
            `Level ${level} finished: ${playMeasurement.duration.toFixed(
                2
            )}ms, ${this.game.state.tick} ticks, ${(
                playMeasurement.duration / this.game.state.tick
            ).toFixed(2)}ms/tick`
        );
    }

    private get _aStar(): AStarFinder {
        if (!this._aStarInstance) {
            this._aStarInstance = new AStarFinder({
                grid: {
                    width: this.game.width,
                    height: this.game.height,
                    matrix: this._grid.tiles.map((row) =>
                        row.map((tile) => (Grid.isWalkable(tile) ? 0 : 1))
                    ),
                },
                diagonalAllowed: false,
                includeStartNode: false,
            });
        }
        return this._aStarInstance;
    }

    private _findPath(start: Position, end: Position): Position[] {
        // performance.mark("findPath-start");
        const path = this._aStar.findPath(start, end);
        // performance.mark("findPath-end");
        // const m = performance.measure(
        //     "findPath",
        //     "findPath-start",
        //     "findPath-end"
        // );
        // console.log(`Path found: ${m.duration.toFixed(2)}ms`);
        return path.map((node) => ({ x: node[0], y: node[1] }));
    }

    private _updateState(): void {
        const playerState = this.game.state.playerState;
        if (playerState?.position && playerState?.surroundings.length > 0) {
            console.log(playerState.surroundings.length);
            console.log(this.game.visibilityRange);
            this._grid.updateSurroundings(
                playerState.position,
                playerState.surroundings,
                this.game.visibilityRange
            );
            console.log(this._grid.toString());
            this._aStarInstance = undefined;
        }
    }

    private async _act(action: DirectedAction): Promise<void> {
        const state = await this.game.act(action);
        this._updateState();
        console.log(
            `Act(${DirectedAction[action]}) state: tick=${state.tick}, level=${
                state.level
            }, status=${GameStatus[state.status]}`
        );
    }

    private async _step(): Promise<void> {
        let direction = this._tryWalkToExit();
        if (direction === DirectedAction.NONE) {
            direction = this._explore();
        }

        // TODO random walk otherwise
        await this._act(direction);
    }

    private _explore(): DirectedAction {
        console.log("Exploring...");
        const unknowns = this._grid.getUnknownPositions();
        const playerPosition = this.game.state.playerState?.position;
        if (unknowns.length === 0 || !playerPosition) {
            return DirectedAction.NONE;
        }
        const d = new Dijkstra(this._grid, playerPosition);
        const unknownDistances: [Position, number][] = unknowns.map((pos) => [
            pos,
            d.getDistance(pos) ?? Infinity,
        ]);
        unknownDistances.sort((a, b) => a[1] - b[1]);
        const closestUnknown = unknownDistances[0][0];
        const path = d.getPath(closestUnknown);
        console.log("closest unknown: ", closestUnknown, path);
        return this._getPathDirection(playerPosition, path);
    }

    private _getDirection(
        fromPosition: Position,
        toPosition: Position
    ): DirectedAction {
        const dx = toPosition.x - fromPosition.x;
        const dy = toPosition.y - fromPosition.y;
        if (dx > 0) {
            return DirectedAction.MOVE_EAST;
        } else if (dx < 0) {
            return DirectedAction.MOVE_WEST;
        } else if (dy > 0) {
            return DirectedAction.MOVE_SOUTH;
        } else if (dy < 0) {
            return DirectedAction.MOVE_NORTH;
        } else {
            return DirectedAction.NONE;
        }
    }

    private _getPathDirection(
        position: Position,
        path: Position[]
    ): DirectedAction {
        const nextPosition = path[0];
        if (!nextPosition) {
            return DirectedAction.NONE;
        }
        return this._getDirection(position, nextPosition);
    }

    private _tryWalkToExit(): DirectedAction {
        const playerPosition = this.game.state.playerState?.position;
        if (!this._grid.exitPosition || !playerPosition) {
            console.log("No exit or player position found");
            return DirectedAction.NONE;
        }
        const path = this._findPath(playerPosition, this._grid.exitPosition);
        if (path.length === 0) {
            console.log("No path to exit found");
            return DirectedAction.NONE;
        }
        console.log("Walking to exit... ", path);
        const direction = this._getPathDirection(playerPosition, path);
        return direction;
    }
}
