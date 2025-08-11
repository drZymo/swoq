pub mod swoq_interface {
    tonic::include_proto!("swoq.interface");
}
mod swoq;

use dotenv::dotenv;
use std::env;
use swoq::GameConnection;

fn get_env_var_i32(key: &str) -> Option<i32> {
    env::var(key).ok().and_then(|val| val.parse::<i32>().ok())
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    dotenv().ok();

    let user_id = env::var("SWOQ_USER_ID")
        .expect("SWOQ_USER_ID environment variable is required, see README.md");
    let user_name = env::var("SWOQ_USER_NAME")
        .expect("SWOQ_USER_NAME environment variable is required, see README.md");
    let host =
        env::var("SWOQ_HOST").expect("SWOQ_HOST environment variable is required, see README.md");
    let level = get_env_var_i32("SWOQ_LEVEL");
    let seed = get_env_var_i32("SWOQ_SEED");
    let replays_folder = env::var("SWOQ_REPLAYS_FOLDER").ok();

    let mut connection = GameConnection::new(user_id, user_name, host, replays_folder).await?;

    let mut game = connection.start(level, seed).await?;

    println!("Game {} started", game.game_id);
    if let Some(seed) = game.seed {
        println!("- seed: {}", seed);
    }
    println!("- map size: {}x{}", game.map_height, game.map_width);
    println!("- visibility range: {}", game.visibility_range);

    let mut move_east = true;
    while game.state.status == swoq_interface::GameStatus::Active as i32 {
        let action = if move_east {
            swoq_interface::DirectedAction::MoveEast
        } else {
            swoq_interface::DirectedAction::MoveSouth
        };
        println!(
            "tick: {}, action: {}",
            game.state.tick,
            action.as_str_name()
        );
        game.act(action).await?;
        move_east = !move_east;
    }

    Ok(())
}
