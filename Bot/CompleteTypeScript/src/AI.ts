import { Game } from "./Game";
import { DirectedAction, GameStatus, Tile } from "./generated/swoq";
import { Grid } from "./Grid";
import { Player, PlayerIndex, PlayerStats, PlayerStepSettings } from "./Player";
import { isMoveAction, posToString, samePos, targetPosition } from "./position";
import { Color, COLOR_TO_PLATE_TILE, COLORS } from "./tile";
import { isDefined, objectEntries } from "./util";

interface AIStats {
    /**
     * Number of times the first act call failed.
     */
    actFailed: number;
    /**
     * Number of times players were hugging (next to each other, but still trying to make a move).
     */
    hugging: number;
    /**
     * Number of times players were hugging for too long, and player 2 was forced to give up its move.
     */
    huggingBroken: number;

    /**
     * Number of times one of the player was prevented from moving
     * to not move to the same target location.
     */
    preventedPlayerCollision: number;

    player1?: PlayerStats;
    player2?: PlayerStats;
}

export type AIGoal = DoorGoal;

export class AI {
    public game: Game;
    public player1?: Player;
    public player2?: Player;
    public grid: Grid;
    public hugging: number = 0;
    public stats: AIStats = {
        actFailed: 0,
        hugging: 0,
        huggingBroken: 0,
        preventedPlayerCollision: 0,
    };
    public goal: AIGoal | undefined;

    public constructor(game: Game) {
        this.game = game;
        this.grid = new Grid(game.mapWidth, game.mapHeight);
    }

    public async play(): Promise<void> {
        let state = this.game.state;
        const startTick = this.game.state.tick;
        performance.mark("start");
        console.log(
            `Start state: tick=${state.tick}, level=${state.level}, seed=${this.game.seed}, status=${
                GameStatus[state.status]
            }`,
        );

        const level = state.level;
        let done = false;
        try {
            while (
                this.game.state.status == GameStatus.ACTIVE &&
                level === this.game.state.level
            ) {
                this._updateState();
                await this._step();
            }
            done = true;
        } finally {
            performance.mark("end");
            const ticks = this.game.state.tick - startTick;
            const playMeasurement = performance.measure("play", "start", "end");
            const result =
                done && this.game.state.status === GameStatus.FINISHED_SUCCESS
                    ? "finished"
                    : "ERRORED";
            console.log(
                `Level ${level} ${result}: ${playMeasurement.duration.toFixed(
                    2,
                )}ms, ${ticks} ticks, ${(
                    playMeasurement.duration / ticks
                ).toFixed(
                    2,
                )}ms/tick, total ${this.game.state.tick} ticks, seed ${this.game.seed}`,
            );
            console.log(this.stats);
        }
    }

    private _updateState(): void {
        const playerState = this.game.state.playerState;
        if (playerState?.position && playerState.position?.x >= 0) {
            if (!this.player1) {
                this.player1 = new Player(
                    this.grid,
                    playerState,
                    this.game.visibilityRange,
                    1,
                );
                this.stats.player1 = this.player1.stats;
            } else {
                this.player1.updateState(
                    playerState,
                    this.game.visibilityRange,
                );
            }
        } else {
            this.player1 = undefined;
        }

        const playerState2 = this.game.state.player2State;
        if (playerState2?.position && playerState2.position?.x >= 0) {
            if (!this.player2) {
                this.player2 = new Player(
                    this.grid,
                    playerState2,
                    this.game.visibilityRange,
                    2,
                );
                this.stats.player2 = this.player2.stats;
            } else {
                this.player2.updateState(
                    playerState2,
                    this.game.visibilityRange,
                );
            }
        } else {
            this.player2 = undefined;
        }
        this.grid.setPlayerPositions(
            this.player1?.position,
            this.player2?.position,
        );

        console.log("Player 1: ", this.player1?.toString());
        console.log("Player 2: ", this.player2?.toString());
        // console.log(this.grid.toString());
    }

    private async _act(
        action1: DirectedAction | undefined,
        action2: DirectedAction | undefined,
    ): Promise<void> {
        const state = await this.game.act(action1, action2);
        console.log(
            `Act(${action1 ? DirectedAction[action1] : undefined},${
                action2 ? DirectedAction[action2] : undefined
            }) state: tick=${state.tick}, level=${state.level}, status=${
                GameStatus[state.status]
            }`,
        );
    }

    private async _step(): Promise<void> {
        const level = this.game.state.level;
        const player1Settings: PlayerStepSettings = {
            // Only allow the player to exit if the other can also leave
            allowExit: !!(
                this.grid.exitPosition &&
                (this.player2?.canReach(this.grid.exitPosition, true) ?? true)
            ),
            canTryStepOnPressurePlate: !this.player2,
            avoidDangerZone:
                level === 21 &&
                (!this.player1?.hasSword || !this.player2?.hasSword),
        };
        const player2Settings: PlayerStepSettings = {
            allowExit: !!(
                this.grid.exitPosition &&
                (this.player1?.canReach(this.grid.exitPosition, true) ?? true)
            ),
            canTryStepOnPressurePlate: level === 21,
            avoidDangerZone:
                level === 21 &&
                (!this.player1?.hasSword || !this.player2?.hasSword),
        };

        // Prevent walking through exit in level 15 when still on pressure plate,
        // because that will block the exit path for the player behind that door.
        if (level === 15) {
            if (player1Settings.allowExit && player2Settings.allowExit) {
                const doors = objectEntries(this.grid.doorPositions).filter(
                    isDefined,
                );
                const doorsWithoutPressurePlates = doors.filter(
                    ([color, positions]) =>
                        this.grid.pressurePlatePositions[color] === undefined,
                );
                const unlockedDoors = doorsWithoutPressurePlates.filter(
                    ([color, positions]) => !this.grid.doorsUnlocked[color],
                );
                if (
                    (this.player1?.onPressurePlateTile !== undefined ||
                        this.player2?.onPressurePlateTile !== undefined) &&
                    unlockedDoors.length > 0
                ) {
                    console.log(
                        "Prevent level 15 exit, still on pressure plate, still locked 'normal' doors",
                    );
                    player1Settings.allowExit = false;
                    player2Settings.allowExit = false;
                }
            }
        }

        // TODO Ugly hack to get playerSettings to show up in toString()
        if (this.player1) {
            this.player1.settings = player1Settings;
        }
        if (this.player2) {
            this.player2.settings = player2Settings;
        }

        let action1: DirectedAction | undefined;
        let action2: DirectedAction | undefined;

        action1 = this.player1?.step(player1Settings);
        action2 = this.player2?.step(player2Settings);
        console.log(
            "Step actions: ",
            DirectedAction[action1 ?? DirectedAction.NONE],
            DirectedAction[action2 ?? DirectedAction.NONE],
        );

        // Forget goal when reached
        if (this.goal) {
            if (!this.player1 || !this.player2) {
                this.goal = undefined;
            } else {
                const doorPlayer =
                    this.goal.doorPlayer === PlayerIndex.Player1
                        ? this.player1
                        : this.player2;
                if (doorPlayer.onDoorTile === this.goal.color) {
                    console.log("Door goal reached.");
                    this.goal = undefined;
                }
            }
        }

        // If one of the players has nothing 'urgent' to do,
        // let's see if we can plan a coordinated action.
        if ((!action1 || !action2) && this.player1 && this.player2) {
            if (!this.goal) {
                console.log(
                    "Players have nothing to do and we don't have a goal yet, looking for a goal...",
                );
                this.goal = this._planCoordinatedGoal();
                if (this.goal) {
                    console.log("New goal set:", this.goal);
                }
            }
            if (this.goal) {
                const ctx: GameContext = {
                    grid: this.grid,
                    player1: this.player1,
                    player2: this.player2,
                };
                const actions = getDoorGoalActions(ctx, this.goal);
                if (!actions) {
                    // TODO unset goal here?
                }
                if (actions) {
                    console.log(
                        `Goal actions: ${DirectedAction[actions[0]]}, ${
                            DirectedAction[actions[1]]
                        }`,
                        "was:",
                        DirectedAction[action1 ?? DirectedAction.NONE],
                        DirectedAction[action2 ?? DirectedAction.NONE],
                    );
                    action1 ??= actions[0];
                    action2 ??= actions[1];
                }
            }
        }

        // If there seems nothing to do, but we saw an enemy before,
        // try to attack it (even if we didn't dare so before).
        // It probably has some loot (key) that we need.
        if (!action1 && !action2) {
            action1 = this.player1?.trySlayEnemy(true);
            action2 = this.player2?.trySlayEnemy(true);
        }

        // Still nothing? Pick a random spot and walk to it.
        if (!action1 && !action2) {
            action1 = this.player1?.tryRandomWalk();
            action2 = this.player2?.tryRandomWalk();
        }

        if (
            !action1 &&
            !action2 &&
            !this.player1?.waiting &&
            !this.player2?.waiting
        ) {
            // TODO Random step/action then?
            throw new Error(`Nothing to do`);
        }

        // Check whether door goal is still active

        // Keep stepping on the plate as long as the door goal is active
        if (this.goal) {
            const platePlayer =
                this.goal.doorPlayer === PlayerIndex.Player1
                    ? this.player2!
                    : this.player1!;
            if (platePlayer.onPressurePlateTile === this.goal.color) {
                if (this.goal.doorPlayer === PlayerIndex.Player1) {
                    console.log("Player 2 waiting for door to be reached");
                    action2 = isMoveAction(action2)
                        ? DirectedAction.NONE
                        : action2;
                } else {
                    console.log("Player 1 waiting for door to be reached");
                    action1 = isMoveAction(action1)
                        ? DirectedAction.NONE
                        : action1;
                }
            }
        }

        // --- Sanity checks ---

        // TODO Never (accidentally) walk into exit with boulder

        // Never step off of pressure plate if other player is still on its door.
        if (this.player1 && this.player2) {
            const p1pt = this.player1.onPressurePlateTile;
            if (p1pt !== undefined && p1pt == this.player2.onDoorTile) {
                console.log(
                    `Waiting for player 2 to leave ${Color[p1pt]} door...`,
                );
                action1 = DirectedAction.NONE;
            }
            const p2pt = this.player2.onPressurePlateTile;
            if (p2pt !== undefined && p2pt == this.player1.onDoorTile) {
                console.log(
                    `Waiting for player 1 to leave ${Color[p2pt]} door...`,
                );
                action2 = DirectedAction.NONE;
            }
        }

        // In level 21, make sure to fetch both swords, i.e. keep stepping on
        // pressure plate until sword-fetching-player steps out of the small
        // room.
        if (level === 21 && this.player1 && this.player2) {
            const p1pt = this.player1.onPressurePlateTile;
            if (p1pt !== undefined) {
                // Player 1 on plate, player 2 fetching sword.
                if (!this.player2.hasSword) {
                    console.log(`Waiting for player 2 to pick up sword`);
                    action1 = DirectedAction.NONE;
                } else {
                    const closestSword = this.player2.getClosestTile(
                        Tile.SWORD,
                    );
                    const distance =
                        (closestSword &&
                            this.player2.dijkstra.getDistance(closestSword)) ||
                        Infinity;
                    if (distance < 5) {
                        console.log(
                            `Waiting for player 2 to clear sword pickup area`,
                        );
                        action1 = DirectedAction.NONE;
                    }
                }
            }
            const p2pt = this.player2.onPressurePlateTile;
            if (p2pt !== undefined) {
                // Player 2 on plate, player 1 fetching sword.
                if (!this.player1.hasSword) {
                    console.log(`Waiting for player 1 to pick up sword`);
                    action2 = DirectedAction.NONE;
                } else {
                    const closestSword = this.player1.getClosestTile(
                        Tile.SWORD,
                    );
                    const distance =
                        (closestSword &&
                            this.player1.dijkstra.getDistance(closestSword)) ||
                        Infinity;
                    if (distance < 5) {
                        console.log(
                            `Waiting for player 1 to clear sword pickup area`,
                        );
                        action2 = DirectedAction.NONE;
                    }
                }
            }
        }

        // Prevent move into each-other
        if (
            this.player1 &&
            this.player2 &&
            isMoveAction(action1) &&
            isMoveAction(action2)
        ) {
            const target1 = targetPosition(this.player1.position, action1);
            const target2 = targetPosition(this.player2.position, action2);
            if (samePos(target1, target2)) {
                this.stats.preventedPlayerCollision++;
                console.log("Preventing player collision");
                action2 = DirectedAction.NONE;
            }
        }

        // If players are too close and trying to walk in
        // opposite direction, they will likely bump into each
        // other and both start moving 'left and right'.
        // Let's call this hugging, and prevent it.
        if (
            this.player1 &&
            this.player2 &&
            isMoveAction(action1) &&
            isMoveAction(action2) &&
            this.player1.dijkstra.getDistance(this.player2.position) === 1
        ) {
            this.hugging++;
            this.stats.hugging++;
            if (this.hugging > 3) {
                this.stats.huggingBroken++;
                action2 = DirectedAction.NONE;
            }
        } else {
            this.hugging = 0;
        }

        if (Math.random() < 0.05) {
            const enemyPos = this.player2?.getClosestTile(Tile.ENEMY);
            const dist =
                enemyPos && this.player2?.dijkstra.getDistance(enemyPos);
            // Don't drop action if close to enemy
            if (!dist || dist > 5) {
                console.log("Drop player 2 action to break potential hugging");
                action2 = DirectedAction.NONE;
            }
        }

        action1 ??= DirectedAction.NONE;
        action2 ??= DirectedAction.NONE;

        try {
            await this._act(
                this.player1 ? action1 : undefined,
                this.player2 ? action2 : undefined,
            );
        } catch (err) {
            console.log(
                `Act(${action1 ? DirectedAction[action1] : undefined},${
                    action2 ? DirectedAction[action2] : undefined
                }) failed:`,
                err instanceof Error ? err.message : err,
            );
            this.stats.actFailed++;
            // TODO Random walk like this might cause exact same problem,
            // probably do something a little smarter.
            await this._act(
                this.player1?.tryRandomWalk(),
                this.player2?.tryRandomWalk(),
            );
        }
    }

    private _planCoordinatedActions():
        | [DirectedAction, DirectedAction]
        | undefined {
        if (!this.player1 || !this.player2) {
            return undefined;
        }
        const steps = [getCoordinatedDoorActions];
        const ctx: GameContext = {
            grid: this.grid,
            player1: this.player1,
            player2: this.player2,
        };
        for (const step of steps) {
            const actions = step(ctx);
            if (actions) {
                return actions;
            }
        }
    }

    private _planCoordinatedGoal(): AIGoal | undefined {
        if (!this.player1 || !this.player2) {
            return undefined;
        }
        const steps = [getCoordinatedDoorGoals];
        const ctx: GameContext = {
            grid: this.grid,
            player1: this.player1,
            player2: this.player2,
        };
        for (const step of steps) {
            const goal = step(ctx);
            if (goal) {
                return goal;
            }
        }
    }
}

interface GameContext {
    grid: Grid;
    player1: Player;
    player2: Player;
}

interface DoorGoal {
    doorPlayer: PlayerIndex;
    color: Color;
}

function getCoordinatedDoorGoals(ctx: GameContext): DoorGoal | undefined {
    for (const color of COLORS) {
        const goal = getCoordinatedDoorGoal(ctx, color);
        if (goal) {
            return goal;
        }
    }
    return undefined;
}

function getCoordinatedDoorActions(
    ctx: GameContext,
): [DirectedAction, DirectedAction] | undefined {
    for (const color of COLORS) {
        const goal = getCoordinatedDoorGoal(ctx, color);
        if (!goal) {
            continue;
        }
        const actions = getDoorGoalActions(ctx, goal);
        if (!actions) {
            continue;
        }
        return actions;
    }
    return undefined;
}

function getCoordinatedDoorGoal(
    { grid, player1, player2 }: GameContext,
    color: Color,
): DoorGoal | undefined {
    const doors = grid.doorPositions[color];
    const plates = grid.pressurePlatePositions[color];
    if (
        plates?.some((pos) => grid.getTile(pos) !== COLOR_TO_PLATE_TILE[color])
    ) {
        // No need to open this door, it's already unlocked
        return undefined;
    }
    if (!doors || !plates) {
        return undefined;
    }
    const p1d = player1.getClosestPosition(doors);
    const p1p = player1.getClosestPosition(plates);
    const p2d = player2.getClosestPosition(doors);
    const p2p = player2.getClosestPosition(plates);

    let doorPlayer: PlayerIndex;
    if (p1d && p2d && p1p && p2p) {
        // Both can reach all, determine most efficient route
        const option1Worst = Math.max(
            player1.dijkstra.getDistance(p1d)!,
            player2.dijkstra.getDistance(p2p)!,
        );
        const option2Worst = Math.max(
            player2.dijkstra.getDistance(p2d)!,
            player1.dijkstra.getDistance(p1p)!,
        );
        if (option1Worst <= option2Worst) {
            doorPlayer = PlayerIndex.Player1;
        } else {
            doorPlayer = PlayerIndex.Player2;
        }
    } else if (p1d && p2p) {
        doorPlayer = PlayerIndex.Player1;
    } else if (p2d && p1p) {
        doorPlayer = PlayerIndex.Player2;
    } else {
        return undefined;
    }
    return { doorPlayer, color };
}

function getDoorGoalActions(
    { grid, player1, player2 }: GameContext,
    goal: DoorGoal,
): [DirectedAction, DirectedAction] | undefined {
    const doors = grid.doorPositions[goal.color];
    const plates = grid.pressurePlatePositions[goal.color];
    if (!doors || !plates) {
        // Can't happen: wouldn't have been able to generate a goal
        throw new Error("No doors or plates found");
    }
    const [p1Goals, p2Goals] =
        goal.doorPlayer === PlayerIndex.Player1
            ? [doors, plates]
            : [plates, doors];
    const [p1Pos, p2Pos] = [
        player1.getClosestPosition(p1Goals),
        player2.getClosestPosition(p2Goals),
    ];
    if (!p1Pos || !p2Pos) {
        // At least one of the players can't reach goal anymore
        return undefined;
    }
    const [p1Act, p2Act] = [
        player1.tryNavigateTo(p1Pos),
        player2.tryNavigateTo(p2Pos),
    ];
    if (!p1Act && !p2Act) {
        // Both players have reached their goal
        return undefined;
    }
    console.log(
        `Door goal: ${PlayerIndex[goal.doorPlayer]} opening ${
            Color[goal.color]
        }. P1 -> ${posToString(p1Pos)}, P2 -> ${posToString(p2Pos)}`,
    );
    return [p1Act ?? DirectedAction.NONE, p2Act ?? DirectedAction.NONE];
}
