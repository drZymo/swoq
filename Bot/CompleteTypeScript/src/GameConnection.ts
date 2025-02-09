import { ChannelCredentials } from "@grpc/grpc-js";
import { GrpcTransport } from "@protobuf-ts/grpc-transport";
import { Game } from "./Game";
import { GameResultError } from "./GameResultError";
import { Result } from "./generated/swoq";
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
        if (response.result !== Result.OK) {
            throw new GameResultError(response.result);
        }
        if (
            response.gameId === undefined ||
            response.width === undefined ||
            response.height === undefined ||
            response.visibilityRange === undefined ||
            response.state === undefined
        ) {
            throw new Error("assertion failed: invalid start response");
        }
        return new Game(
            this.client,
            response.gameId,
            response.width,
            response.height,
            response.visibilityRange,
            response.state
        );
    }
}
