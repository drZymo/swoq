import { ChannelCredentials } from "@grpc/grpc-js";
import { GrpcTransport } from "@protobuf-ts/grpc-transport";
import { mkdir } from "fs/promises";
import * as path from "path";
import { Game } from "./Game";
import { GameStartResultError } from "./GameResultError";
import {
    StartRequest,
    StartResponse,
    StartResult,
} from "./generated/swoq";
import { GameServiceClient, IGameServiceClient } from "./generated/swoq.client";
import { ReplayFile } from "./ReplayFile";
import { formatDate, sleep } from "./util";

export class GameConnection implements Disposable {
    private readonly transport: GrpcTransport;
    private readonly client: IGameServiceClient;
    private readonly replayFolder: string | undefined;

    public readonly userId: string;
    public readonly userName: string;

    private async makeReplayFile(
        request: StartRequest,
        response: StartResponse,
    ): Promise<ReplayFile> {
        if (!this.replayFolder) {
            throw new Error("Replay folder not specified");
        }

        // Determine file name
        const dateTimeStr = formatDate(new Date());

        const filename = path.join(
            this.replayFolder,
            `${request.userName} - ${dateTimeStr} - ${response.gameId}.swoq`,
        );

        // Create the directory if it doesn't exist
        const directory = path.dirname(filename);
        await mkdir(directory, { recursive: true });

        const replayFile = await ReplayFile.create(
            filename,
            request,
            response,
        );
        return replayFile;
    }

    constructor(
        host: string,
        userId: string,
        userName: string,
        replayFolder?: string,
    ) {
        this.userId = userId;
        this.userName = userName;
        this.replayFolder = replayFolder;
        this.transport = new GrpcTransport({
            host,
            channelCredentials: ChannelCredentials.createInsecure(),
        });
        this.client = new GameServiceClient(this.transport);
    }

    public async start(level?: number): Promise<Game> {
        const request: StartRequest = {
            userId: this.userId,
            userName: this.userName,
            level,
        };

        let response: StartResponse;
        while (true) {
            response = await this.client.start(request).response;
            if (response.result === StartResult.OK) {
                break;
            }
            if (response.result === StartResult.QUEST_QUEUED) {
                console.log(`Quest queued, retrying ...`);
                continue;
            }
            throw new GameStartResultError(response.result);
        }

        // Open replay file if we need one
        await using stack = new AsyncDisposableStack();
        const replayFile = this.replayFolder
            ? stack.use(await this.makeReplayFile(request, response))
            : undefined;

        // Construct game, pass ownership of replay file to it
        const game = new Game(this.client, response, replayFile);
        stack.move();
        return game;
    }

    public [Symbol.dispose](): void {
        this.transport.close();
    }
}
