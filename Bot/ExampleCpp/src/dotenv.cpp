#include "dotenv.hpp"
#include <fstream>
#include <iostream>
#include <ranges>
#include <format>
#include <cstdlib>

std::expected<void, std::string> load_dotenv() {
    std::ifstream env_file(".env");
    if (!env_file.is_open()) {
        return std::unexpected("Failed to open .env file");
    }

    std::string line;
    while (std::getline(env_file, line)) {
        // Remove comments
        if (auto comment_pos = line.find('#'); comment_pos != std::string::npos) {
            line.resize(comment_pos);
        }

        // Trim whitespace using ranges
        auto trimmed = line | std::views::drop_while([](unsigned char ch) { return std::isspace(ch); })
                            | std::views::reverse
                            | std::views::drop_while([](unsigned char ch) { return std::isspace(ch); })
                            | std::views::reverse;
        line.assign(std::ranges::begin(trimmed), std::ranges::end(trimmed));

        if (line.empty()) {
            continue;
        }

        // Split on '='
        auto eq_pos = line.find('=');
        if (eq_pos == std::string::npos) {
            continue;
        }

        std::string_view key{line.data(), eq_pos};
        std::string_view value{line.data() + eq_pos + 1};

        // Remove surrounding quotes from value
        if (!value.empty() && value.front() == '"' && value.back() == '"') {
            value = value.substr(1, value.size() - 2);
        }

        // Set environment variable
        if (setenv(std::string{key}.c_str(), std::string{value}.c_str(), 1) != 0) {
            return std::unexpected(std::format("Failed to set environment variable: {}", key));
        }
    }

    return {};
}

std::optional<int> get_env_int(std::string_view name) {
    auto value = std::getenv(name.data());
    if (!value) return std::nullopt;

    try {
        return std::stoi(value);
    } catch (const std::exception& ex) {
        std::println(std::cerr, "Invalid integer value for {}: {}", name, ex.what());
        return std::nullopt;
    }
}

std::optional<std::string> get_env_str(std::string_view name) {
    auto value = std::getenv(name.data());
    if (!value) return std::nullopt;

    return std::string(value);
}

std::string require_env_str(std::string_view name) {
    auto value = get_env_str(name);
    if (!value) {
        std::println(std::cerr, "Environment variable {} not set", name);
        std::exit(-1);
    }
    return *value;
}
