use crate::swoq_interface::game_service_client::GameServiceClient;
use crate::swoq_interface::{
    self, ActRequest, ActResponse, StartRequest, StartResponse, StartResult,
};
use prost::{Message, bytes::BytesMut};
use std::convert::TryFrom;
use std::error::Error;
use std::fmt;
use std::fs::File;
use std::io::{self, Write};
use std::path::Path;
use time::{OffsetDateTime, format_description};
use tonic::transport::Channel;

#[derive(Debug)]
pub enum SwoqError {
    StartFailed { result: swoq_interface::StartResult },
    ActFailed { result: swoq_interface::ActResult },
}

impl fmt::Display for SwoqError {
    fn fmt(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
        match self {
            SwoqError::StartFailed { result } => {
                write!(formatter, "Start failed (result {})", result.as_str_name())
            }
            SwoqError::ActFailed { result } => {
                write!(formatter, "Act failed (result {})", result.as_str_name())
            }
        }
    }
}

impl Error for SwoqError {}

pub struct GameConnection {
    user_id: String,
    user_name: String,
    replays_folder: Option<String>,
    client: GameServiceClient<Channel>,
}

impl GameConnection {
    pub async fn new(
        user_id: String,
        user_name: String,
        host: String,
        replays_folder: Option<String>,
    ) -> Result<Self, Box<dyn std::error::Error>> {
        let client = GameServiceClient::connect(format!("http://{}", host)).await?;
        Ok(GameConnection {
            user_id,
            user_name,
            replays_folder,
            client,
        })
    }

    pub async fn start(
        &mut self,
        level: Option<i32>,
        seed: Option<i32>,
    ) -> Result<Game, Box<dyn std::error::Error>> {
        loop {
            let request = StartRequest {
                user_id: self.user_id.clone(),
                level,
                seed,
            };
            let response = self.client.start(request.clone()).await?.into_inner();

            let result = StartResult::try_from(response.result).unwrap();

            match result {
                StartResult::Ok => {
                    let replay_file = self.replays_folder.as_ref().and_then(|folder| {
                        ReplayFile::new(&self.user_name, folder, &request, &response).ok()
                    });
                    return Ok(Game::new(&mut self.client, response, replay_file));
                }
                StartResult::QuestQueued => {
                    println!("Quest queued, retrying ...");
                }
                _ => {
                    return Err(Box::new(SwoqError::StartFailed { result }));
                }
            }
        }
    }
}

pub struct Game<'c> {
    client: &'c mut GameServiceClient<Channel>,
    replay_file: Option<ReplayFile>,
    pub game_id: String,
    pub map_height: i32,
    pub map_width: i32,
    pub visibility_range: i32,
    pub state: swoq_interface::State,
    pub seed: Option<i32>,
}

impl<'c> Game<'c> {
    fn new(
        client: &'c mut GameServiceClient<Channel>,
        response: StartResponse,
        replay_file: Option<ReplayFile>,
    ) -> Self {
        Game {
            client,
            replay_file,
            game_id: response.game_id.clone().unwrap(),
            map_height: response.map_height.unwrap(),
            map_width: response.map_width.unwrap(),
            visibility_range: response.visibility_range.unwrap(),
            state: response.state.clone().unwrap(),
            seed: response.seed,
        }
    }

    pub async fn act(
        &mut self,
        action: swoq_interface::DirectedAction,
    ) -> Result<(), Box<dyn std::error::Error>> {
        let request = ActRequest {
            game_id: self.game_id.clone(),
            action: Some(action as i32),
        };
        let response = self.client.act(request.clone()).await?.into_inner();
        let result = swoq_interface::ActResult::try_from(response.result).unwrap();

        if let Some(ref mut replay_file) = self.replay_file {
            replay_file.append(&request, &response)?;
        }

        if result != swoq_interface::ActResult::Ok {
            return Err(Box::new(SwoqError::ActFailed { result }));
        }

        self.state = response.state.unwrap();
        return Ok(());
    }
}

struct ReplayFile {
    file: File,
}

impl ReplayFile {
    pub fn new(
        user_name: &str,
        replays_folder: &str,
        start_request: &StartRequest,
        start_response: &StartResponse,
    ) -> Result<Self, std::io::Error> {
        let now = OffsetDateTime::now_local().ok().unwrap();
        let date_time_str = now
            .format(
                &format_description::parse("[year][month][day]-[hour][minute][second]").unwrap(),
            )
            .unwrap();
        let game_id = start_response.game_id.clone().unwrap();

        let filename = Path::new(replays_folder).join(format!(
            "{} - {} - {}.swoq",
            user_name, date_time_str, game_id
        ));

        if let Some(parent) = filename.parent() {
            if !parent.exists() {
                std::fs::create_dir_all(parent)?;
            }
        }

        let file = File::create(filename)?;
        let mut replay_file = ReplayFile { file };

        let header = swoq_interface::ReplayHeader {
            user_name: user_name.to_string(),
            date_time: now
                .format(&format_description::well_known::Iso8601::DEFAULT)
                .unwrap(),
        };
        replay_file.write_delimited_message(&header)?;

        replay_file.write_delimited_message(start_request)?;
        replay_file.write_delimited_message(start_response)?;

        Ok(replay_file)
    }

    fn append(&mut self, act_request: &ActRequest, act_response: &ActResponse) -> io::Result<()> {
        self.write_delimited_message(act_request)?;
        self.write_delimited_message(act_response)?;
        Ok(())
    }

    fn write_delimited_message<T: Message>(&mut self, message: &T) -> io::Result<()> {
        let mut buf = Vec::new();
        message.encode(&mut buf)?;

        let mut varint_buf = BytesMut::new();
        prost::encode_length_delimiter(buf.len(), &mut varint_buf)?;

        self.file.write_all(&varint_buf)?;
        self.file.write_all(&buf)?;
        self.file.flush()?;
        Ok(())
    }
}
