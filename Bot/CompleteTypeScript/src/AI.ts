import { Game } from "./Game";
import { DirectedAction, GameStatus, Tile } from "./generated/swoq";
import { Grid } from "./Grid";
import { Player } from "./Player";
import { Color } from "./tile";

export class AI {
    public game: Game;
    public player?: Player;
    public grid: Grid;

    public constructor(game: Game) {
        this.game = game;
        this.grid = new Grid(game.width, game.height);
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
            const playMeasurement = performance.measure("play", "start", "end");
            const result =
                done && this.game.state.status === GameStatus.FINISHED_SUCCESS
                    ? "finished"
                    : "ERRORED";
            console.log(
                `Level ${level} ${result}: ${playMeasurement.duration.toFixed(
                    2
                )}ms, ${this.game.state.tick} ticks, ${(
                    playMeasurement.duration / this.game.state.tick
                ).toFixed(2)}ms/tick`
            );
        }
    }

    private _updateState(): void {
        const playerState = this.game.state.playerState;
        if (playerState) {
            if (!this.player) {
                this.player = new Player(
                    this.grid,
                    playerState,
                    this.game.visibilityRange
                );
            } else {
                this.player.updateState(playerState, this.game.visibilityRange);
            }
        } else {
            this.player = undefined;
        }

        console.log("Player: ", this.player?.toString());
        console.log(this.grid.toString());
    }

    private async _act(action: DirectedAction): Promise<void> {
        const state = await this.game.act(action);
        this.player?.actionPerformed(action);
        console.log(
            `Act(${DirectedAction[action]}) state: tick=${state.tick}, level=${
                state.level
            }, status=${GameStatus[state.status]}`
        );
    }

    private async _step(): Promise<void> {
        // TODO: early levels: pick up key asap
        const steps = [
            () => this._focusedAction(),
            // TODO In early levels (2?) and level 10, we can pick up key ASAP
            () => this._tryPickupSword(),
            () => this._tryPickupHealth(),
            () => this._tryWalkToExit(),
            () => this._tryOpenDoor(),
            // TODO Tweak moment of enemy slaying? Is it always necessary to slay it?
            () => this._trySlayEnemy(),
            () => this._tryExplore(),
        ];
        let action: DirectedAction | undefined = undefined;
        for (const step of steps) {
            action = step();
            if (action !== undefined) {
                break;
            }
        }

        // TODO random walk otherwise
        if (!action && !this.player?.waiting) {
            throw new Error(`Nothing to do`);
        }

        // Sanity checks
        // TODO Never walk into exit with boulder
        await this._act(action ?? DirectedAction.NONE);
    }

    private _trySlayEnemy(): DirectedAction | undefined {
        return this.player?.trySlayEnemy();
    }

    private _tryPickupSword(): DirectedAction | undefined {
        // Pick up any sword. Typically, we'll start to see a sword
        // as soon as it becomes available, so it's a short stroll.
        return this.player?.navigateToTile(Tile.SWORD);
    }

    private _tryPickupHealth(): DirectedAction | undefined {
        return this.player?.navigateToTile(Tile.HEALTH);
    }

    private _focusedAction(): DirectedAction | undefined {
        return this.player?.tryFocused();
    }

    private _tryOpenDoor(): DirectedAction | undefined {
        const player = this.player;
        if (!player) {
            return undefined;
        }
        return (
            player.tryOpenDoor(Color.Red) ||
            player.tryOpenDoor(Color.Green) ||
            player.tryOpenDoor(Color.Blue)
        );
    }

    private _tryExplore(): DirectedAction | undefined {
        return this.player?.tryExplore();
    }

    private _tryWalkToExit(): DirectedAction | undefined {
        return this.player?.tryWalkExit();
    }
}
