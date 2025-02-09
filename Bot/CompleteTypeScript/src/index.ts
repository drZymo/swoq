import "dotenv/config";
import "source-map-support/register";

import { AI } from "./AI";
import { Game } from "./Game";
import { GameConnection } from "./GameConnection";
import { GameStatus } from "./generated/swoq";

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
    while (game.state.status === GameStatus.ACTIVE) {
        // Keep playing every level until the game is over
        const ai = new AI(game);
        await ai.play();
    }
}

main().catch((err) => {
    console.error(err);
    process.exit(1);
});
