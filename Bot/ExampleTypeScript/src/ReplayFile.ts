import { BinaryWriter } from "@protobuf-ts/runtime";
import { FileHandle, open } from "fs/promises";
import {
    ActRequest,
    ActResponse,
    StartRequest,
    StartResponse,
} from "./generated/swoq";

export class ReplayFile implements AsyncDisposable {
    private _file: FileHandle | undefined;
    public readonly fileName: string;

    public static async create(
        fileName: string,
        startRequest: StartRequest,
        startResponse: StartResponse
    ): Promise<ReplayFile> {
        const file = await open(fileName, "a+");
        const replayFile = new ReplayFile(fileName, file);
        try {
            await replayFile._writeDelimited(
                StartRequest.toBinary(startRequest)
            );
            await replayFile._writeDelimited(
                StartResponse.toBinary(startResponse)
            );
            return replayFile;
        } catch (err) {
            await file.close();
            throw err;
        }
    }

    private constructor(fileName: string, file: FileHandle) {
        this.fileName = fileName;
        this._file = file;
    }

    public async append(
        request: ActRequest,
        response: ActResponse
    ): Promise<void> {
        await this._writeDelimited(ActRequest.toBinary(request));
        await this._writeDelimited(ActResponse.toBinary(response));
    }

    public async close(): Promise<void> {
        if (this._file === undefined) {
            throw new Error("File not open");
        }
        await this._file.close();
        this._file = undefined;
    }

    public async [Symbol.asyncDispose](): Promise<void> {
        if (this._file === undefined) {
            return;
        }
        await this.close();
    }

    private async _writeDelimited(message: Uint8Array): Promise<void> {
        if (this._file === undefined) {
            throw new Error("File not open");
        }
        const bytes = new BinaryWriter().bytes(message).finish();
        await this._file.write(bytes);
    }
}
