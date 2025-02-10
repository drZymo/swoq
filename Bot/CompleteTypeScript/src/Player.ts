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
import {
    Color,
    COLOR_TO_DOOR_TILE,
    COLOR_TO_KEY_TILE,
    COLOR_TO_PLATE_TILE,
} from "./tile";

export enum PlayerFocus {
    BoulderExplore = 1,
    OpenDoorPressurePlate = 2,
}

export class Player {
    grid: Grid;
    state: PlayerState;
    position!: Position;
    waiting: boolean = false;
    focus?: PlayerFocus;

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
        this.waiting = false;

        this.grid.updateSurroundings(
            this.position,
            this.state.surroundings,
            visibilityRange
        );
    }

    public toString(): string {
        const elems = [
            `pos=${posToString(this.position)}`,
            `inventory=${Inventory[this.state.inventory ?? Inventory.NONE]}`,
            this.focus ? `focus=${PlayerFocus[this.focus]}` : undefined,
        ].filter((e) => !!e);
        return `Player(${elems.join(", ")})`;
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

    public getClosestTile(tile: Tile): Position | undefined {
        const positions = this.grid.tilePositions[tile];
        if (!positions) {
            return undefined;
        }
        return this.getClosestPosition(positions);
    }

    public getClosestPressurePlate(color: Color): Position | undefined {
        // TODO perhaps remember the plates better, as they may 'disappear' when
        // something else steps on it.
        return this.getClosestTile(COLOR_TO_PLATE_TILE[color]);
    }

    public getClosestUnknown(): Position | undefined {
        const onlyUnknowns = this.grid.tilePositions[Tile.UNKNOWN] ?? [];
        const boulderUnknowns =
            this.grid.tilePositions[Tile.BOULDER]?.filter(
                (pos) =>
                    this.grid.getAllNeighborsOfType(pos, Tile.UNKNOWN).length >
                    0
            ) ?? [];
        console.log("boulderUnknowns", boulderUnknowns);
        if (onlyUnknowns.length === 0 && boulderUnknowns.length === 0) {
            return undefined;
        }
        // Also look for boulders that have unknown-ness around them, might hide
        // e.g. the exit
        return this.getClosestPosition([...onlyUnknowns, ...boulderUnknowns]);
    }

    public getClosestPosition(positions: Position[]): Position | undefined {
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

    public navigateAndUse(pos: Position): DirectedAction | undefined {
        const path = this.dijkstra.getPath(pos);
        if (path.length < 2) {
            return undefined;
        }
        const dir = getPathDirection(path);
        if (dir && path.length === 2) {
            // Right next to target
            return moveToUse(dir);
        }
        return dir;
    }

    public navigateTo(pos: Position): DirectedAction | undefined {
        const path = this.dijkstra.getPath(pos);
        return getPathDirection(path);
    }

    public tryOpenDoor(color: Color): DirectedAction | undefined {
        return (
            this.tryOpenDoorWithKeys(color) ??
            this.tryOpenDoorWithPressurePlateAndBoulder(color)
        );
    }

    public tryOpenDoorWithKeys(color: Color): DirectedAction | undefined {
        // Try to open the door if we have the key
        const door = this.getClosestTile(COLOR_TO_DOOR_TILE[color]);
        if (this.state.inventory === COLOR_TO_KEY_INVENTORY[color]) {
            if (!door) {
                return undefined;
            }
            console.log(`Opening ${Color[color]} door at`, door);
            return this.navigateAndUse(door);
        }
        const key = this.getClosestTile(COLOR_TO_KEY_TILE[color]);
        // If we can't reach both the key and door, don't try
        if (!door || !key) {
            return undefined;
        }
        // We can reach both, but we don't have the key, fetch that first
        console.log(`Picking up ${Color[color]} key at`, key);
        return this.navigateTo(key);
    }

    public tryOpenDoorWithPressurePlateAndBoulder(
        color: Color
    ): DirectedAction | undefined {
        // Try to open the door if we have the key
        const door = this.getClosestTile(COLOR_TO_DOOR_TILE[color]);
        const plate = this.getClosestPressurePlate(color);
        // If we can't reach a door and plate, don't try
        if (!door || !plate) {
            return undefined;
        }
        if (this.hasBoulder) {
            // Already picked up a boulder, go put it on the plate
            console.log(`Placing boulder on ${Color[color]} plate at`, plate);
            return this.navigateAndUse(plate);
        }
        // Need to pick up boulder first
        // TODO Compute more optimal path (boulder closest to plate)
        const boulder = this.getClosestTile(Tile.BOULDER);
        if (!boulder) {
            return this._tryOpenDoorWithPressurePlate(plate);
        }
        if (!!this.state.inventory) {
            console.warn(
                "Can't pick up boulder for pressure plate, already have inventory"
            );
            return undefined;
        }
        console.log(
            `Picking up boulder at`,
            boulder,
            `for ${Color[color]} pressure plate at`,
            plate
        );
        return this.navigateAndUse(boulder);
    }

    private _tryOpenDoorWithPressurePlate(
        plate: Position
    ): DirectedAction | undefined {
        return this.navigateTo(plate);
    }

    public tryExplore(): DirectedAction | undefined {
        const closestUnknown = this.getClosestUnknown();
        if (!closestUnknown) {
            console.debug("No unknowns left");
            return undefined;
        }
        if (this.grid.getTile(closestUnknown) === Tile.BOULDER) {
            console.log("Explore behind boulder", closestUnknown);
            this.focus = PlayerFocus.BoulderExplore;
            return this.navigateAndUse(closestUnknown);
        }
        console.log("Explore to", closestUnknown);
        return this.navigateTo(closestUnknown);
    }

    public tryFocused(): DirectedAction | undefined {
        if (!this.focus) {
            return undefined;
        }
        // Focus completed?
        if (this.focus === PlayerFocus.BoulderExplore && !this.hasBoulder) {
            console.log("Focus completed: boulder explored.");
            this.focus = undefined;
        }
        // Execute focus actions
        if (this.focus === PlayerFocus.BoulderExplore) {
            console.log("Focus: boulder explore dropping boulder...");
            return this.dropBoulder();
        }
    }

    public get hasBoulder() {
        return this.state.inventory === Inventory.BOULDER;
    }

    public tryWalkExit(): DirectedAction | undefined {
        if (!this.grid.exitPosition) {
            console.log("No exit found yet");
            return undefined;
        }

        if (this.hasBoulder) {
            console.log("Dropping boulder before exiting...");
            return this.dropBoulder();
        }

        const dir = this.navigateTo(this.grid.exitPosition);
        if (dir === undefined) {
            console.log("Exit unreachable");
        } else {
            console.log("Walking to exit... ");
        }
        return dir;
    }

    public dropBoulder(): DirectedAction | undefined {
        const dropLocation = this.grid.getAllNeighborsOfType(
            this.position,
            Tile.EMPTY
        )[0];
        if (!dropLocation) {
            // TODO: find better options further away if necessary
            console.warn(
                `Can't drop boulder near ${posToString(
                    this.position
                )}: no empty spots!`
            );
            return undefined;
        }
        return this.navigateAndUse(dropLocation);
    }

    public actionPerformed(action: DirectedAction): void {
        // TODO unneeded?
    }
}
