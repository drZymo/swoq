import { ChannelCredentials } from "@grpc/grpc-js";
import { GrpcTransport } from "@protobuf-ts/grpc-transport";
import { Game } from "./Game";
import { GameStartResultError } from "./GameResultError";
import { StartResult } from "./generated/swoq";
import { GameServiceClient, IGameServiceClient } from "./generated/swoq.client";

export class GameConnection {
    private readonly client: IGameServiceClient;

    public readonly userId: string;

    constructor(host: string, userId: string) {
        this.userId = userId;
        const transport = new GrpcTransport({
            host,
            channelCredentials: ChannelCredentials.createInsecure(),
        });
        this.client = new GameServiceClient(transport);
    }

    public async start(level?: number): Promise<Game> {
        const { response } = await this.client.start({
            userId: this.userId,
            level,
        });
        // TODO Add queued quest handling
        if (response.result !== StartResult.OK) {
            throw new GameStartResultError(response.result);
        }
        if (
            response.gameId === undefined ||
            response.mapWidth === undefined ||
            response.mapHeight === undefined ||
            response.visibilityRange === undefined ||
            response.state === undefined
        ) {
            throw new Error("assertion failed: invalid start response");
        }
        return new Game(
            this.client,
            response.gameId,
            response.mapWidth,
            response.mapHeight,
            response.visibilityRange,
            response.state
        );
    }
}
