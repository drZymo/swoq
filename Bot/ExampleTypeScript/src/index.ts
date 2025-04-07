import "disposablestack/auto";
import "dotenv/config";
import "source-map-support/register";

import path from "path";
import { Game } from "./Game";
import { GameConnection } from "./GameConnection";
import { DirectedAction, GameStatus } from "./generated/swoq";
import { requireEnvVar } from "./util";

async function main(): Promise<void> {
    using gameConnection = new GameConnection(
        requireEnvVar("SWOQ_HOST"),
        requireEnvVar("SWOQ_USER_ID"),
        requireEnvVar("SWOQ_USER_NAME"),
        path.join(
            __dirname,
            "..",
            process.env.SWOQ_REPLAYS_FOLDER ?? "Replays",
        ),
    );

    const level = process.env.SWOQ_LEVEL
        ? parseInt(process.env.SWOQ_LEVEL)
        : undefined;
    if (level === undefined) {
        console.log("Starting quest...");
    } else {
        console.log(`Starting training for level ${level}...`);
    }
    await using game = await gameConnection.start(level);
    await play(game);
}

async function play(game: Game): Promise<void> {
    let state = game.state;
    console.log(
        `Start state: tick=${state.tick}, level=${state.level}, status=${
            GameStatus[state.status]
        }`
    );

    let moveEast = true;
    while (state.status == GameStatus.ACTIVE) {
        const action = moveEast ? DirectedAction.MOVE_EAST : DirectedAction.MOVE_SOUTH;
        state = await game.act(
            action
        );
        console.log(
            `Act(${DirectedAction[action]}): tick=${state.tick}, level=${state.level}, status=${
                GameStatus[state.status]
            }`
        );
        moveEast = !moveEast;
    }

    console.log(`Done.`);
}

main().catch((err) => {
    console.error(err);
    process.exit(1);
});
