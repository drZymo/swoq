package main

import (
    "fmt"
    "log"
    "os"

    "github.com/joho/godotenv"

    "bot/proto/swoq"
)

func main() {
    err := godotenv.Load()
    if err != nil {
        fmt.Println("Error loading .env file")
    }

    host := os.Getenv("SWOQ_HOST")
    userId := os.Getenv("SWOQ_USER_ID")
    userName := os.Getenv("SWOQ_USER_NAME")
    replaysFolder := os.Getenv("SWOQ_REPLAYS_FOLDER")
    gameConn, err := NewGameConnection(host, userId, userName, replaysFolder)
    if err != nil {
        log.Fatalf("Failed to connect to game service: %v", err)
    }
    defer gameConn.Close()

    level := GetenvOptionalInt32("SWOQ_LEVEL")
    seed := GetenvOptionalInt32("SWOQ_SEED")
    game, err := gameConn.Start(level, seed)
    if err != nil {
        log.Fatalf("Failed to start game: %v", err)
    }
    defer game.Close()
    log.Printf("Game %s started", game.gameId)
    log.Printf("- seed: %d", game.seed)
    log.Printf("- map size: %dx%d", game.mapHeight, game.mapWidth)

    moveEast := true
    for game.state.Status == swoq.GameStatus_GAME_STATUS_ACTIVE {
        var action swoq.DirectedAction
        if moveEast {
            action = swoq.DirectedAction_DIRECTED_ACTION_MOVE_EAST
        } else {
            action = swoq.DirectedAction_DIRECTED_ACTION_MOVE_SOUTH
        }
        log.Printf("tick: %d, action: %s", game.state.Tick, action.String())
        err := game.Act(action)
        if err != nil {
            log.Fatalf("Failed to perform action: %v", err)
        }
        moveEast = !moveEast
    }
}
