import { Game } from "./Game";
import { DirectedAction, GameStatus } from "./generated/swoq";
import { Grid } from "./Grid";
import { Player, PlayerStats, PlayerStepSettings } from "./Player";
import { isMoveAction, samePos, targetPosition } from "./position";

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

    player1?: PlayerStats;
    player2?: PlayerStats;
}

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
    };

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
        const player1Settings: PlayerStepSettings = {
            // Only allow the player to exit if the other can also leave
            allowExit: !!(
                this.grid.exitPosition &&
                (this.player2?.canReach(this.grid.exitPosition) ?? true)
            ),
        };
        const player2Settings: PlayerStepSettings = {
            allowExit: !!(
                this.grid.exitPosition &&
                (this.player1?.canReach(this.grid.exitPosition) ?? true)
            ),
        };
        let action1 = this.player1?.step(player1Settings);
        let action2 = this.player2?.step(player2Settings);

        // If there seems nothing to do, but we saw an enemy before,
        // try to attack it (even if we didn't dare so before).
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

        // Sanity checks
        // TODO Never (accidentally) walk into exit with boulder

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
                action2 = DirectedAction.NONE;
            }
        }

        // If players are too close and trying to walk in
        // opposite direction, they're
        if (
            this.player1 &&
            this.player2 &&
            isMoveAction(action1) &&
            isMoveAction(action2) &&
            this.player1?.dijkstra.getDistance(this.player2.position) === 1
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
            await this._act(
                this.player1?.tryRandomWalk(),
                this.player2?.tryRandomWalk(),
            );
        }
    }
}
