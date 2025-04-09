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
    isWalkable,
} from "./tile";

export enum PlayerFocus {
    // Picked up a boulder to peek behind it, need to put it back down ASAP
    BoulderExplore = 1,
    // Started fighting the enemy, so don't back down now
    Fighting = 2,
}

export interface PlayerStats {
    randomTargets: number;
    randomActs: number;
}

export interface PlayerStepSettings {
    allowExit: boolean;
}

export class Player {
    grid: Grid;
    state: PlayerState;
    position!: Position;
    waiting: boolean = false;
    focus?: PlayerFocus;
    startedAttack: boolean = false;
    randomTarget: Position | undefined;
    index: 1 | 2;
    stats: PlayerStats = {
        randomActs: 0,
        randomTargets: 0,
    };

    private _dijkstraInstance: Dijkstra | undefined;

    constructor(
        grid: Grid,
        playerState: PlayerState,
        visibilityRange: number,
        index: 1 | 2
    ) {
        this.grid = grid;
        this.state = playerState;
        this.index = index;
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
            `health=${this.state.health}`,
            this.state.hasSword ? `has_sword` : undefined,
            this.focus ? `focus=${PlayerFocus[this.focus]}` : undefined,
        ].filter((e) => !!e);
        return `Player(${elems.join(", ")})`;
    }

    public log(...args: any[]): void {
        console.log(`[Player ${this.index}]`, ...args);
    }

    public get dijkstra(): Dijkstra {
        if (!this._dijkstraInstance) {
            this._dijkstraInstance = new Dijkstra(
                this.grid,
                this.position,
                (pos) => {
                    const tile = this.grid.getTile(pos);
                    if (tile === Tile.SWORD && this.hasSword) {
                        // Prevent RESULT_INVENTORY_FULL error when walking over
                        // another sword
                        // TODO Perhaps do the same for keys?
                        return false;
                    }
                    return isWalkable(tile);
                }
            );
        }
        return this._dijkstraInstance;
    }

    public canReach(pos: Position): boolean {
        return this.dijkstra.getDistance(pos) !== undefined;
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
        this.log("boulderUnknowns", boulderUnknowns);
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

    public navigateToTile(tile: Tile): DirectedAction | undefined {
        const pos = this.getClosestTile(tile);
        if (!pos) {
            return undefined;
        }
        this.log(`Navigating to ${Tile[tile]} at`, pos);
        return this.navigateTo(pos);
    }

    public step(settings: PlayerStepSettings): DirectedAction | undefined {
        // TODO: early levels: pick up key asap
        const steps = [
            () => this.tryFocused(),
            // TODO In early levels (2?) and level 10, we can pick up key ASAP
            () => this.tryPickupSword(),
            () => this.tryPickupHealth(),
            () => (settings.allowExit ? this.tryWalkToExit() : undefined),
            () => this.tryOpenDoors(),
            // TODO Tweak moment of enemy slaying? Is it always necessary to slay it?
            () => this.trySlayEnemy(),
            // TODO Try to explore away from the other player
            () => this.tryExplore(),
        ];
        let action: DirectedAction | undefined = undefined;
        for (const step of steps) {
            action = step();
            if (action !== undefined) {
                break;
            }
        }
        return action;
    }

    public tryPickupSword(): DirectedAction | undefined {
        // Pick up any sword. Typically, we'll start to see a sword
        // as soon as it becomes available, so it's a short stroll.
        if (this.hasSword) {
            return undefined;
        }
        return this.navigateToTile(Tile.SWORD);
    }

    public tryPickupHealth(): DirectedAction | undefined {
        return this.navigateToTile(Tile.HEALTH);
    }

    public tryOpenDoors(): DirectedAction | undefined {
        return (
            this.tryOpenDoor(Color.Red) ||
            this.tryOpenDoor(Color.Green) ||
            this.tryOpenDoor(Color.Blue)
        );
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
            this.log(`Opening ${Color[color]} door at`, door);
            return this.navigateAndUse(door);
        }
        const key = this.getClosestTile(COLOR_TO_KEY_TILE[color]);
        // If we can't reach both the key and door, don't try
        if (!door || !key) {
            return undefined;
        }
        // We can reach both, but we don't have the key, fetch that first
        this.log(`Picking up ${Color[color]} key at`, key);
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
            this.log(`Placing boulder on ${Color[color]} plate at`, plate);
            return this.navigateAndUse(plate);
        }
        // Need to pick up boulder first
        // TODO Compute more optimal path (boulder closest to plate)
        const boulder = this.getClosestTile(Tile.BOULDER);
        if (!boulder) {
            return this._tryOpenDoorWithPressurePlate(plate, color);
        }
        if (!!this.state.inventory) {
            this.log(
                "WARNING: Can't pick up boulder for pressure plate, already have inventory"
            );
            return undefined;
        }
        this.log(
            `Picking up boulder at`,
            boulder,
            `for ${Color[color]} pressure plate at`,
            plate
        );
        return this.navigateAndUse(boulder);
    }

    private _tryOpenDoorWithPressurePlate(
        plate: Position,
        color: Color
    ): DirectedAction | undefined {
        this.log(`Opening ${Color[color]} door with pressure plate at`, plate);
        return this.navigateTo(plate);
    }

    public tryExplore(): DirectedAction | undefined {
        const closestUnknown = this.getClosestUnknown();
        if (!closestUnknown) {
            this.log("No unknowns left");
            return undefined;
        }
        if (this.grid.getTile(closestUnknown) === Tile.BOULDER) {
            this.log("Explore behind boulder", closestUnknown);
            this.focus = PlayerFocus.BoulderExplore;
            return this.navigateAndUse(closestUnknown);
        }
        this.log("Explore to", closestUnknown);
        return this.navigateTo(closestUnknown);
    }

    public tryFocused(): DirectedAction | undefined {
        if (!this.focus) {
            return undefined;
        }
        // Focus completed?
        if (this.focus === PlayerFocus.BoulderExplore && !this.hasBoulder) {
            this.log("Focus completed: boulder explored.");
            this.focus = undefined;
        }
        // Execute focus actions
        if (this.focus === PlayerFocus.BoulderExplore) {
            this.log("Focus: boulder explore dropping boulder...");
            return this.dropBoulder();
        }
    }

    public get hasBoulder(): boolean {
        return this.state.inventory === Inventory.BOULDER;
    }

    public get hasSword(): boolean {
        return !!this.state.hasSword;
    }

    public tryWalkToExit(): DirectedAction | undefined {
        if (!this.grid.exitPosition) {
            this.log("No exit found yet");
            return undefined;
        }

        if (this.hasBoulder) {
            this.log("Dropping boulder before exiting...");
            return this.dropBoulder();
        }

        const dir = this.navigateTo(this.grid.exitPosition);
        if (dir === undefined) {
            this.log("Exit unreachable");
        } else {
            this.log("Walking to exit... ");
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
            this.log(
                `WARNING: Can't drop boulder near ${posToString(
                    this.position
                )}: no empty spots!`
            );
            return undefined;
        }
        return this.navigateAndUse(dropLocation);
    }

    public trySlayEnemy(
        desperate: boolean = false
    ): DirectedAction | undefined {
        if (!this.hasSword) {
            return undefined;
        }
        const enemy = this.getClosestTile(Tile.ENEMY);
        if (!enemy) {
            return undefined;
        }
        if (!desperate && !this.startedAttack && (this.state.health ?? 0) < 5) {
            // Don't start a fight unless we already started it, or we feel strong enough
            this.log("Ignoring enemy for now");
            return undefined;
        }
        if (!desperate && (this.state.health ?? 0) <= 1) {
            this.log(`WARNING: Need to flee, health=${this.state.health}`);
            this.startedAttack = false;
            return undefined;
        }
        if (desperate) {
            this.log(`Desperately slaying enemy at`, enemy);
        } else {
            this.log(`Slaying enemy at`, enemy);
        }
        this.startedAttack = true;
        return this.navigateAndUse(enemy);
    }

    tryRandomWalk(): DirectedAction | undefined {
        if (this.randomTarget) {
            const distance = this.dijkstra.getDistance(this.randomTarget);
            if (distance === undefined) {
                this.log("Random target no longer reachable, picking new one");
                this.randomTarget = undefined;
            } else if (distance === 0) {
                this.log("Random target reached, picking new one");
                this.randomTarget = undefined;
            }
        }
        if (!this.randomTarget) {
            const reachable = this.dijkstra.getRandomReachablePosition();
            if (!reachable) {
                this.log("Random walk target not found");
                return undefined;
            }
            this.stats.randomTargets++;
            this.randomTarget = reachable;
        }
        this.log("Random walk to", this.randomTarget);
        this.stats.randomActs++;
        return this.navigateTo(this.randomTarget);
    }
}
