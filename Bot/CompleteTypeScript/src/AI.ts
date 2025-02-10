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
        while (
            this.game.state.status == GameStatus.ACTIVE &&
            level === this.game.state.level
        ) {
            this._updateState();
            await this._step();
        }

        performance.mark("end");
        const playMeasurement = performance.measure("play", "start", "end");
        const result =
            this.game.state.status === GameStatus.FINISHED_SUCCESS
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
        console.log(
            `Act(${DirectedAction[action]}) state: tick=${state.tick}, level=${
                state.level
            }, status=${GameStatus[state.status]}`
        );
    }

    private async _step(): Promise<void> {
        let direction = this._tryWalkToExit();
        if (direction === DirectedAction.NONE) {
            direction = this._tryOpenDoor();
        }
        if (direction === DirectedAction.NONE) {
            direction = this._tryExplore();
        }

        // TODO random walk otherwise
        await this._act(direction);
    }

    private _tryOpenDoor(): DirectedAction {
        const player = this.player;
        if (!player) {
            return DirectedAction.NONE;
        }
        return (
            player.tryOpenDoor(Color.Red) ||
            player.tryOpenDoor(Color.Green) ||
            player.tryOpenDoor(Color.Blue)
        );
    }

    private _tryExplore(): DirectedAction {
        const closestUnknown = this.player?.getClosest(Tile.UNKNOWN);
        if (!closestUnknown) {
            console.debug("No unknowns left");
            return DirectedAction.NONE;
        }
        console.log("Explore to", closestUnknown);
        if (!this.player) {
            return DirectedAction.NONE;
        }
        return this.player.navigateTo(closestUnknown);
    }

    private _tryWalkToExit(): DirectedAction {
        if (!this.grid.exitPosition) {
            console.log("No exit found yet");
            return DirectedAction.NONE;
        }
        if (!this.player) {
            console.log("No player");
            return DirectedAction.NONE;
        }

        const dir = this.player.navigateTo(this.grid.exitPosition);
        if (dir === DirectedAction.NONE) {
            console.log("Exit unreachable");
        } else {
            console.log("Walking to exit... ");
        }
        return dir;
    }
}
