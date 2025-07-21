#pragma once

#include <string>
#include <grpcpp/grpcpp.h>
#include "src/swoq.grpc.pb.h"
#include <format>
#include <optional>
#include <expected>
#include <fstream>

namespace Swoq {

    namespace fs = std::filesystem;

    class Game;
    class ReplayFile;

    class GameConnection {
    public:
        GameConnection(std::string_view user_id, std::string_view user_name, std::string_view host, std::optional<std::string_view> replays_folder);
        GameConnection() = delete;
        ~GameConnection() = default;

        std::expected<std::unique_ptr<Game>, std::string> start(std::optional<int> level = std::nullopt, std::optional<int> seed = std::nullopt);

    private:
        std::string m_user_id;
        std::string m_user_name;
        std::optional<std::string> m_replays_folder;

        std::shared_ptr<grpc::Channel> m_channel;
        std::shared_ptr<Swoq::Interface::GameService::Stub> m_stub;
    };

    class ReplayFile {
    public:
        static std::expected<std::unique_ptr<ReplayFile>, std::string> create(
            std::string_view replays_folder,
            const Swoq::Interface::StartRequest& request,
            const Swoq::Interface::StartResponse& response);

        explicit ReplayFile(std::ofstream&& stream);
        ~ReplayFile() = default;
        ReplayFile(const ReplayFile&) = delete;
        ReplayFile& operator=(const ReplayFile&) = delete;
        ReplayFile(ReplayFile&&) = default;
        ReplayFile& operator=(ReplayFile&&) = default;

        std::expected<void, std::string> append(
            const Swoq::Interface::ActRequest& request,
            const Swoq::Interface::ActResponse& response);

    private:
        std::ofstream stream_;
    };

    class Game {
    public:
        Game(std::shared_ptr<Swoq::Interface::GameService::Stub> stub,
            const Swoq::Interface::StartResponse& start_response,
            std::unique_ptr<ReplayFile>&& replay_file);

        const std::string& game_id() const;
        int map_width() const;
        int map_height() const;
        int visibility_range() const;
        int seed() const;
        const Swoq::Interface::State& state() const;

        std::expected<void, std::string> act(Swoq::Interface::DirectedAction action);

    private:
        std::shared_ptr<Swoq::Interface::GameService::Stub> m_stub;
        std::unique_ptr<ReplayFile> m_replay_file;
        Swoq::Interface::StartResponse m_start_response;
        Swoq::Interface::State m_state;
    };

} // namespace Swoq

template <>
struct std::formatter<Swoq::Interface::StartResult> {
    constexpr auto parse(std::format_parse_context& ctx) { return ctx.begin(); }
    auto format(const Swoq::Interface::StartResult& result, std::format_context& ctx) const {
        using enum Swoq::Interface::StartResult;
        std::string s = "<UNKNOWN>";
        switch (result) {
            case START_RESULT_OK: s = "OK"; break;
            case START_RESULT_INTERNAL_ERROR: s = "INTERNAL_ERROR"; break;
            case START_RESULT_UNKNOWN_USER: s = "UNKNOWN_USER"; break;
            case START_RESULT_INVALID_LEVEL: s = "INVALID_LEVEL"; break;
            case START_RESULT_QUEST_QUEUED: s = "QUEST_QUEUED"; break;
            case START_RESULT_NOT_ALLOWED: s = "NOT_ALLOWED"; break;
        }
        return std::format_to(ctx.out(), "{}", s);
    }
};

template <>
struct std::formatter<Swoq::Interface::ActResult> {
    constexpr auto parse(std::format_parse_context& ctx) { return ctx.begin(); }
    auto format(const Swoq::Interface::ActResult& result, std::format_context& ctx) const {
        using enum Swoq::Interface::ActResult;
        std::string s = "<UNKNOWN>";
        switch (result) {
            case ACT_RESULT_OK: s = "OK"; break;
            case ACT_RESULT_INTERNAL_ERROR: s = "INTERNAL_ERROR"; break;
            case ACT_RESULT_UNKNOWN_GAME_ID: s = "UNKNOWN_GAME_ID"; break;
            case ACT_RESULT_MOVE_NOT_ALLOWED: s = "MOVE_NOT_ALLOWED"; break;
            case ACT_RESULT_UNKNOWN_ACTION: s = "UNKNOWN_ACTION"; break;
            case ACT_RESULT_GAME_FINISHED: s = "GAME_FINISHED"; break;
        }
        return std::format_to(ctx.out(), "{}", s);
    }
};

template <>
struct std::formatter<Swoq::Interface::DirectedAction> {
    constexpr auto parse(std::format_parse_context& ctx) { return ctx.begin(); }
    auto format(const Swoq::Interface::DirectedAction& action, std::format_context& ctx) const {
        using enum Swoq::Interface::DirectedAction;
        std::string s;
        switch (action) {
            case DIRECTED_ACTION_NONE: s = "NONE"; break;
            case DIRECTED_ACTION_MOVE_NORTH:s = "MOVE_NORTH"; break;
            case DIRECTED_ACTION_MOVE_EAST: s = "MOVE_EAST"; break;
            case DIRECTED_ACTION_MOVE_SOUTH: s = "MOVE_SOUTH"; break;
            case DIRECTED_ACTION_MOVE_WEST: s = "MOVE_WEST"; break;
            default: s = "<UNKNOWN>"; break;
        }
        return std::format_to(ctx.out(), "{}", s);
    }
};
