#pragma once

#include <expected>
#include <string>
#include <optional>

void load_dotenv();

std::optional<int> get_env_int(std::string_view name);
std::optional<std::string> get_env_str(std::string_view name);
std::string require_env_str(std::string_view name);
