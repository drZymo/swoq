#include <print>
#include "dotenv.hpp"
#include "swoq.hpp"

using namespace Swoq::Interface;
using namespace Swoq;

int main(int /*argc*/, char** /*argv*/) {
    // Load .env file
    load_dotenv();

    // Create a new GameConnection instance using env variables
    auto user_id = require_env_str("SWOQ_USER_ID");
    auto user_name = require_env_str("SWOQ_USER_NAME");
    auto host = require_env_str("SWOQ_HOST");
    auto replays_folder = get_env_str("SWOQ_REPLAYS_FOLDER");
    GameConnection connection(user_id, user_name, host, replays_folder);

    // Start a new Game using env variables
    auto level = get_env_int("SWOQ_LEVEL");
    auto seed = get_env_int("SWOQ_SEED");
    auto start_result = connection.start(level, seed);
    if (!start_result) {
        std::println(std::cerr, "Failed to start game: {}", start_result.error());
        return -1;
    }
    auto& game = (*start_result);

    // Show game stats
    std::println("Game {} started", game->game_id());
    std::println("- seed: {}", game->seed());
    std::println("- map size: {}x{}", game->map_height(), game->map_width());

    // Game loop
    auto move_east = true;
    while (game->state().status() == GameStatus::GAME_STATUS_ACTIVE) {
        // Determine action
        auto action = move_east ? DirectedAction::DIRECTED_ACTION_MOVE_EAST : DirectedAction::DIRECTED_ACTION_MOVE_SOUTH;
        std::println("tick: {}, action: {}", game->state().tick(), action);

        // Act
        auto result = game->act(action);
        if (!result) {
            std::println(std::cerr, "Action failed: {}", result.error());
            return -1;
        }

        // Prepare for next action
        move_east = !move_east;
    }

    return 0;
}
