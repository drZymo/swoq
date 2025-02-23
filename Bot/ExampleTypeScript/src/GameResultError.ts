import { StartResult, ActResult } from "./generated/swoq";

export class GameStartResultError extends Error {
    public readonly result: StartResult;

    constructor(result: StartResult) {
        super(`GameStartResult error: ${StartResult[result]}`);
        this.result = result;
    }
}

export class GameActResultError extends Error {
    public readonly result: ActResult;

    constructor(result: ActResult) {
        super(`GameActResult error: ${ActResult[result]}`);
        this.result = result;
    }
}
