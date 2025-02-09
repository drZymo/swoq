import { GameResultError } from "./GameResultError";
import { DirectedAction, Result, State } from "./generated/swoq";
import { IGameServiceClient } from "./generated/swoq.client";

export class Game {
    private readonly client: IGameServiceClient;

    public readonly gameId: string;
    public readonly width: number;
    public readonly height: number;
    public readonly visibilityRange: number;
    public state: State;

    constructor(
        client: IGameServiceClient,
        gameId: string,
        width: number,
        height: number,
        visibilityRange: number,
        state: State
    ) {
        this.client = client;
        this.gameId = gameId;
        this.width = width;
        this.height = height;
        this.visibilityRange = visibilityRange;
        this.state = state;
    }

    public async act(action: DirectedAction): Promise<State> {
        const { response } = await this.client.act({
            gameId: this.gameId,
            action,
        });
        if (response.state !== undefined) {
            this.state = response.state;
        }
        if (response.result !== Result.OK) {
            // TODO what to do with this.state here? (Esp if/when it is undefined on response)
            throw new GameResultError(response.result);
        }
        return this.state;
    }
}
