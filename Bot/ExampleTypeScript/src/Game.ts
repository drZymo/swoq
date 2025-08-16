import { GameActResultError } from "./GameResultError";
import {
    ActRequest,
    ActResult,
    DirectedAction,
    StartResponse,
    State,
} from "./generated/swoq";
import { IGameServiceClient } from "./generated/swoq.client";
import { ReplayFile } from "./ReplayFile";

export class Game implements AsyncDisposable {
    private readonly client: IGameServiceClient;
    private readonly replayFile?: ReplayFile;

    public readonly gameId: string;
    public readonly mapWidth: number;
    public readonly mapHeight: number;
    public readonly visibilityRange: number;
    public readonly seed: number | undefined;
    public state: State;

    constructor(
        client: IGameServiceClient,
        response: StartResponse,
        replayFile?: ReplayFile,
    ) {
        this.client = client;
        if (
            response.gameId === undefined ||
            response.mapWidth === undefined ||
            response.mapHeight === undefined ||
            response.visibilityRange === undefined ||
            response.state === undefined
        ) {
            throw new Error("assertion failed: invalid start response");
        }
        this.gameId = response.gameId;
        this.mapWidth = response.mapWidth;
        this.mapHeight = response.mapHeight;
        this.visibilityRange = response.visibilityRange;
        this.state = response.state;
        this.replayFile = replayFile;
        this.seed = response.seed;
    }

    public async act(action: DirectedAction | undefined): Promise<State> {
        const request: ActRequest = {
            gameId: this.gameId,
            action,
        };
        const { response } = await this.client.act(request);
        if (response.state !== undefined) {
            this.state = response.state;
        }

        await this.replayFile?.append(request, response);

        if (response.result !== ActResult.OK) {
            // TODO what to do with this.state here? (Esp if/when it is undefined on response)
            throw new GameActResultError(response.result);
        }
        return this.state;
    }

    public async [Symbol.asyncDispose](): Promise<void> {
        await this.replayFile?.[Symbol.asyncDispose]();
    }
}
