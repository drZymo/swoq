import "dotenv/config";
import "source-map-support/register";

import { Game } from "./Game";
import { GameConnection } from "./GameConnection";
import { DirectedAction, GameStatus } from "./generated/swoq";

async function main(): Promise<void> {
    if (!process.env.SWOQ_HOST || !process.env.SWOQ_USER_ID) {
        console.error(
            "SWOQ_HOST and SWOQ_USER_ID environment variables are required, see README.md"
        );
        process.exit(1);
    }

    const gameConnection = new GameConnection(
        process.env.SWOQ_HOST,
        process.env.SWOQ_USER_ID
    );

    const level = process.env.SWOQ_LEVEL
        ? parseInt(process.env.SWOQ_LEVEL)
        : undefined;
    if (level === undefined) {
        console.log("Starting quest...");
    } else {
        console.log(`Starting training for level ${level}...`);
    }
    const game = await gameConnection.start(level);
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
        state = await game.act(
            moveEast ? DirectedAction.MOVE_EAST : DirectedAction.MOVE_SOUTH
        );
        console.log(
            `Act state: tick=${state.tick}, level=${state.level}, status=${
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
