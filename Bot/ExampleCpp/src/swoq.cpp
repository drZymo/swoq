#include "swoq.hpp"

namespace Swoq {

    using namespace Swoq::Interface;

    GameConnection::GameConnection(
        std::string_view user_id,
        std::string_view user_name,
        std::string_view host,
        std::optional<std::string_view> replays_folder)
        : m_user_id(user_id)
        , m_user_name(user_name)
        , m_replays_folder(replays_folder) {
        m_channel = grpc::CreateChannel(std::string{host}, grpc::InsecureChannelCredentials());
        m_stub = GameService::NewStub(m_channel);
    }

    std::expected<std::unique_ptr<Game>, std::string> GameConnection::start(std::optional<int> level, std::optional<int> seed) {
        grpc::ClientContext context;
        StartRequest start_request;
        start_request.set_userid(m_user_id);
        start_request.set_username(m_user_name);
        if (level) start_request.set_level(*level);
        if (seed) start_request.set_seed(*seed);
        StartResponse start_response;

        auto status = m_stub->Start(&context, start_request, &start_response);
        if (!status.ok()) {
            return std::unexpected(std::format("gRPC error {} - {}", std::to_string(status.error_code()), status.error_message()));
        }

        while (start_response.result() == StartResult::START_RESULT_QUEST_QUEUED) {
            std::println(std::cerr, "Quest queued, retrying ...");
            status = m_stub->Start(&context, start_request, &start_response);
            if (!status.ok()) {
                return std::unexpected(std::format("gRPC error {} - {}", std::to_string(status.error_code()), status.error_message()));
            }
        }

        if (start_response.result() != StartResult::START_RESULT_OK) {
            return std::unexpected(std::format("Start failed (result {})", start_response.result()));
        }

        std::unique_ptr<ReplayFile> replay_file;
        if (m_replays_folder) {
            auto replay_result = ReplayFile::create(*m_replays_folder, start_request, start_response);
            if (!replay_result) {
                return std::unexpected(std::format("Failed to create ReplayFile: {}", replay_result.error()));
            }
            replay_file = std::move(replay_result.value());
        }

        return std::make_unique<Game>(m_stub, start_response, std::move(replay_file));
    }

    std::expected<std::unique_ptr<ReplayFile>, std::string> ReplayFile::create(
        std::string_view replays_folder,
        const StartRequest& request,
        const StartResponse& response) {
        // Determine file name
        auto now = std::chrono::system_clock::now();
        auto local_time = std::chrono::zoned_time{std::chrono::current_zone(), now};
        fs::path folder = fs::absolute(replays_folder);
        std::string filename = (folder / (std::format("{} - {:%Y%m%d-%H%M%S} - {}.swoq", 
            request.username(), local_time, response.gameid()))).string();

        // Create directory if needed
        std::error_code ec;
        if (!fs::create_directories(fs::path(filename).parent_path(), ec) && ec) {
            return std::unexpected(std::format("Failed to create directories: {}", ec.message()));
        }

        // Create file
        std::ofstream stream(filename, std::ios::binary | std::ios::out);
        if (!stream.is_open()) {
            return std::unexpected("Failed to open file stream");
        }

        // Construct ReplayFile and write initial messages
        auto replay_file = std::make_unique<ReplayFile>(std::move(stream));
        if (auto result = replay_file->write_delimited(request); !result) {
            return std::unexpected(std::format("Failed to write StartRequest: {}", result.error()));
        }
        if (auto result = replay_file->write_delimited(response); !result) {
            return std::unexpected(std::format("Failed to write StartResponse: {}", result.error()));
        }

        return replay_file;
    }

    ReplayFile::ReplayFile(std::ofstream&& stream)
        : m_stream(std::move(stream))
        , m_zero_copy_output(&m_stream)
        , m_coded_output(&m_zero_copy_output)
    {}

    std::expected<void, std::string> ReplayFile::append(const ActRequest& request, const ActResponse& response) {
        if (auto result = write_delimited(request); !result) {
            return std::unexpected(std::format("Failed to write ActRequest: {}", result.error()));
        }
        if (auto result = write_delimited(response); !result) {
            return std::unexpected(std::format("Failed to write ActResponse: {}", result.error()));
        }
        return {};
    }

    std::expected<void, std::string> ReplayFile::write_delimited(const google::protobuf::Message& msg) {
        m_coded_output.WriteVarint32(msg.ByteSizeLong());
        if (!msg.SerializeToCodedStream(&m_coded_output)) {
            return std::unexpected("SerializeToCodedStream failed");
        }
        return {};
    }

    Game::Game(
        std::shared_ptr<GameService::Stub> stub,
        const StartResponse& start_response,
        std::unique_ptr<ReplayFile>&& replay_file)
        : m_stub(stub)
        , m_replay_file(std::move(replay_file))
        , m_start_response(start_response)
        , m_state(start_response.state()) { }

    const std::string& Game::game_id() const { return m_start_response.gameid(); }
    int Game::map_width() const { return m_start_response.mapwidth(); }
    int Game::map_height() const { return m_start_response.mapheight(); }
    int Game::visibility_range() const { return m_start_response.visibilityrange(); }
    int Game::seed() const { return m_start_response.seed(); }
    const State& Game::state() const { return m_state; }

    std::expected<void, std::string> Game::act(DirectedAction action) {
        grpc::ClientContext context;
        ActRequest act_request;
        act_request.set_gameid(game_id());
        act_request.set_action(action);
        ActResponse act_response;
        auto status = m_stub->Act(&context, act_request, &act_response);
        if (!status.ok()) {
            return std::unexpected(std::format("gRPC error {} - {}", std::to_string(status.error_code()), status.error_message()));
        }

        if (m_replay_file) m_replay_file->append(act_request, act_response);

        if (act_response.result() != ActResult::ACT_RESULT_OK) {
            return std::unexpected(std::format("Act failed (result {})", act_response.result()));
        }
        m_state = act_response.state();
        return {};
    }

} // namespace Swoq
