import { Result } from "./generated/swoq";

export class GameResultError extends Error {
    public readonly result: Result;

    constructor(result: Result) {
        super(`GameResult error: ${Result[result]}`);
        this.result = result;
    }
}
