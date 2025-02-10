import { Dijkstra } from "./Dijkstra";
import {
    DirectedAction,
    Inventory,
    PlayerState,
    Position,
    Tile,
} from "./generated/swoq";
import { Grid } from "./Grid";
import { COLOR_TO_KEY_INVENTORY } from "./inventory";
import { getPathDirection, moveToUse, posToString } from "./position";
import { Color, COLOR_TO_DOOR_TILE, COLOR_TO_KEY_TILE } from "./tile";

export class Player {
    grid: Grid;
    state: PlayerState;
    position!: Position;

    private _dijkstraInstance: Dijkstra | undefined;

    constructor(grid: Grid, playerState: PlayerState, visibilityRange: number) {
        this.grid = grid;
        this.state = playerState;
        this.updateState(playerState, visibilityRange);
    }

    updateState(playerState: PlayerState, visibilityRange: number): void {
        this.state = playerState;
        if (!playerState.position) {
            // TODO Can this really happen?
            throw new Error("Can't update Player, missing position");
        }
        this.position = playerState.position!;
        this._dijkstraInstance = undefined;

        this.grid.updateSurroundings(
            this.position,
            this.state.surroundings,
            visibilityRange
        );
    }

    public toString(): string {
        return `Player(pos=${posToString(this.position)}, inventory=${
            Inventory[this.state.inventory ?? Inventory.NONE]
        })`;
    }

    public get dijkstra(): Dijkstra {
        if (!this._dijkstraInstance) {
            this._dijkstraInstance = new Dijkstra(this.grid, this.position);
        }
        return this._dijkstraInstance;
    }

    public canReach(tile: Tile): boolean {
        const positions = this.grid.tilePositions[tile];
        if (!positions) {
            return false;
        }
        const d = this.dijkstra;
        return positions.some((pos) => d.getDistance(pos) !== undefined);
    }

    public getClosest(tile: Tile): Position | undefined {
        const positions = this.grid.tilePositions[tile];
        if (!positions) {
            return undefined;
        }
        const d = this.dijkstra;
        const cands = positions.map((pos): [Position, number] => [
            pos,
            d.getDistance(pos) ?? Infinity,
        ]);
        cands.sort((c1, c2) => c1[1] - c2[1]);
        return cands[0][0];
    }

    public getPathTo(pos: Position): Position[] {
        return this.dijkstra.getPath(pos);
    }

    public navigateAndUse(pos: Position): DirectedAction {
        const path = this.dijkstra.getPath(pos);
        if (path.length < 2) {
            return DirectedAction.NONE;
        }
        const dir = getPathDirection(path);
        if (path.length === 2) {
            // Right next to target
            return moveToUse(dir);
        }
        return dir;
    }

    public navigateTo(pos: Position): DirectedAction {
        const path = this.dijkstra.getPath(pos);
        return getPathDirection(path);
    }

    public tryOpenDoor(color: Color): DirectedAction {
        // Try to open the door if we have the key
        const door = this.getClosest(COLOR_TO_DOOR_TILE[color]);
        if (this.state.inventory === COLOR_TO_KEY_INVENTORY[color]) {
            if (!door) {
                return DirectedAction.NONE;
            }
            console.log(`Opening ${Color[color]} door at`, door);
            return this.navigateAndUse(door);
        }
        const key = this.getClosest(COLOR_TO_KEY_TILE[color]);
        // If we can't reach both the key and door, don't try
        if (!door || !key) {
            return DirectedAction.NONE;
        }
        // We can reach both, but we don't have the key, fetch that first
        console.log(`Picking up ${Color[color]} key at`, key);
        return this.navigateTo(key);
    }
}
