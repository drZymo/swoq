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
import { getPathDirection, moveToUse, posToString, samePos } from "./position";
import {
    Color,
    COLOR_TO_DOOR_TILE,
    COLOR_TO_KEY_TILE,
    COLOR_TO_PLATE_TILE,
    COLORS,
    isWalkable,
} from "./tile";

/**
 * Short-term focus of the player.
 */
export enum PlayerFocus {
    // Picked up a boulder to peek behind it, need to put it back down ASAP
    BoulderExplore = 1,
}

/**
 * Long(er) term coordinated target of the player set by external AI.
 */
export enum PlayerTargetType {
    NavigateAndUse,
    NavigateTo,
}

export type PlayerTarget = {
    type: PlayerTargetType.NavigateAndUse | PlayerTargetType.NavigateTo;
    target: Position;
};

export interface PlayerStats {
    randomTargets: number;
    randomActs: number;
}

export interface PlayerStepSettings {
    /**
     * Whether the player is allowed to exit the game.
     * For multi-player, make it wait for the other player,
     * as it may still be needed to unblock an obstacle.
     */
    allowExit: boolean;

    /**
     * Whether the player is allowed to try and use a pressure
     * plate by stepping on it, to open a door.
     *
     * This is only sensible in the first few levels. In later
     * levels, we want explicit coordination from the AI to
     * allow this.
     */
    canTryStepOnPressurePlate: boolean;

    /**
     * Whether to avoid the bottom-right area of the map, where lots
     * of enemies lurk, as long as we can't find them.
     */
    avoidDangerZone: boolean;
}

export enum PlayerIndex {
    Player1 = 0,
    Player2 = 1,
}

export class Player {
    grid: Grid;
    state: PlayerState;
    position!: Position;
    waiting: boolean = false;
    focus?: PlayerFocus;
    target?: PlayerTarget;
    startedAttack: boolean = false;
    randomTarget: Position | undefined;
    index: 1 | 2;
    stats: PlayerStats = {
        randomActs: 0,
        randomTargets: 0,
    };
    settings?: PlayerStepSettings; // TODO: Remove, ugly hack just to show them in toString

    private _dijkstraInstance: Dijkstra | undefined;
    private _dijkstraLTInstance: Dijkstra | undefined;

    constructor(
        grid: Grid,
        playerState: PlayerState,
        visibilityRange: number,
        index: 1 | 2,
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
        this._dijkstraLTInstance = undefined;
        this.waiting = false;

        this.grid.updateSurroundings(
            this.position,
            this.state.surroundings,
            visibilityRange,
        );
    }

    public toString(): string {
        const elems = [
            `pos=${posToString(this.position)}`,
            `inventory=${Inventory[this.state.inventory ?? Inventory.NONE]}`,
            `health=${this.state.health}`,
            this.state.hasSword ? `has_sword` : undefined,
            this.settings?.allowExit ? "allowExit" : undefined,
            this.settings?.canTryStepOnPressurePlate
                ? "canTryStepOnPressurePlate"
                : undefined,
            this.settings?.avoidDangerZone ? "avoidDangerZone" : undefined,
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
                },
            );
        }
        return this._dijkstraInstance;
    }

    public get dijkstraLongterm(): Dijkstra {
        if (!this._dijkstraLTInstance) {
            this._dijkstraLTInstance = new Dijkstra(
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
                    if (tile === Tile.PLAYER) {
                        // Player is 'walkable' for long-term planning,
                        // to prevent getting stuck when one player blocks
                        // the door for the other. In this case, the blocking
                        // player will just walk through the door.
                        return true;
                    }
                    return isWalkable(tile);
                },
            );
        }
        return this._dijkstraLTInstance;
    }

    public canReach(pos: Position, longTerm: boolean = false): boolean {
        const dijkstra = longTerm ? this.dijkstraLongterm : this.dijkstra;
        return dijkstra.getDistance(pos) !== undefined;
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
        let onlyUnknowns = this.grid.tilePositions[Tile.UNKNOWN] ?? [];
        if (this.settings?.avoidDangerZone) {
            // Enemies lurk in the bottom right area
            onlyUnknowns = onlyUnknowns.filter(
                (pos) =>
                    !(
                        pos.x >= this.grid.width - 11 &&
                        pos.y >= this.grid.height - 18
                    ),
            );
        }
        const boulderUnknowns =
            this.grid.tilePositions[Tile.BOULDER]?.filter(
                (pos) =>
                    this.grid.getAllNeighborsOfType(pos, Tile.UNKNOWN).length >
                    0,
            ) ?? [];
        //this.log("boulderUnknowns", boulderUnknowns);
        if (onlyUnknowns.length === 0 && boulderUnknowns.length === 0) {
            return undefined;
        }
        // Also look for boulders that have unknown-ness around them, might hide
        // e.g. the exit
        return this.getClosestPosition([...onlyUnknowns, ...boulderUnknowns]);
    }

    public getClosestPosition(positions: Position[]): Position | undefined {
        if (positions.length === 0) {
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

    public tryNavigateTo(pos: Position): DirectedAction | undefined {
        const path = this.dijkstra.getPath(pos);
        if (path.length == 2) {
            // The last element may not be reachable yet, so wait here
            const target = path[1];
            if (!this.grid.isWalkable(target)) {
                return DirectedAction.NONE;
            }
        }
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
            () => this.tryOpenDoors(settings.canTryStepOnPressurePlate),
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

    public tryOpenDoors(
        canTryStepOnPressurePlate: boolean,
    ): DirectedAction | undefined {
        return (
            this.tryOpenDoor(Color.Red, canTryStepOnPressurePlate) ||
            this.tryOpenDoor(Color.Green, canTryStepOnPressurePlate) ||
            this.tryOpenDoor(Color.Blue, canTryStepOnPressurePlate)
        );
    }

    public tryOpenDoor(
        color: Color,
        canTryStepOnPressurePlate: boolean,
    ): DirectedAction | undefined {
        return (
            this.tryOpenDoorWithKeys(color) ??
            this.tryOpenDoorWithPressurePlateAndBoulder(
                color,
                canTryStepOnPressurePlate,
            )
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
        color: Color,
        canTryStepOnPressurePlate: boolean,
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
        let boulders = this.grid.tilePositions[Tile.BOULDER] ?? [];
        // Don't consider boulders that are already placed on a pressure plate
        // (because most likely we put them before)
        const pressurePlates = Object.values(
            this.grid.pressurePlatePositions,
        ).flat();
        boulders = boulders.filter(
            (pos) => !pressurePlates.some((p) => samePos(p, pos)),
        );
        // TODO Compute more optimal path (boulder closest to plate)
        const boulder = this.getClosestPosition(boulders);
        if (!boulder) {
            if (canTryStepOnPressurePlate) {
                return this._tryOpenDoorWithPressurePlate(plate, color);
            } else {
                this.log(
                    `Don't see a boulder to pick up for pressure ${Color[color]} plate.`,
                );
                return undefined;
            }
        }
        if (!!this.state.inventory) {
            this.log(
                "WARNING: Can't pick up boulder for pressure plate, already have inventory",
            );
            return undefined;
        }
        this.log(
            `Picking up boulder at`,
            boulder,
            `for ${Color[color]} pressure plate at`,
            plate,
        );
        return this.navigateAndUse(boulder);
    }

    private _tryOpenDoorWithPressurePlate(
        plate: Position,
        color: Color,
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

    public get onDoorTile(): Color | undefined {
        // TODO Optimize? Note: can't just look at tile, because
        // that will 'disappear' when the door opens.
        return COLORS.find((color) =>
            this.grid.doorPositions[color]?.some((pos) =>
                samePos(this.position, pos),
            ),
        );
    }

    public get onPressurePlateTile(): Color | undefined {
        // TODO Optimize? Note: can't just look at tile, because
        // that will 'disappear' when a player steps on it.
        return COLORS.find((color) =>
            this.grid.pressurePlatePositions[color]?.some((pos) =>
                samePos(this.position, pos),
            ),
        );
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
            Tile.EMPTY,
        )[0];
        if (!dropLocation) {
            // TODO: find better options further away if necessary
            this.log(
                `WARNING: Can't drop boulder near ${posToString(
                    this.position,
                )}: no empty spots!`,
            );
            return undefined;
        }
        return this.navigateAndUse(dropLocation);
    }

    public trySlayEnemy(
        desperate: boolean = false,
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
