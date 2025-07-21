cmake_minimum_required(VERSION 3.10)
project(bot)

enable_language(CXX)

find_package(Protobuf CONFIG REQUIRED)
message(STATUS "Using Protobuf ${Protobuf_VERSION}")

find_package(gRPC CONFIG REQUIRED)
message(STATUS "Using gRPC ${gRPC_VERSION}")

add_executable(bot
  src/main.cpp
  src/swoq.proto
  src/swoq.cpp
  src/dotenv.cpp
)

target_link_libraries(bot PUBLIC
  protobuf::libprotobuf
  gRPC::grpc
  gRPC::grpc++
)

target_include_directories(bot PUBLIC
  ${CMAKE_CURRENT_BINARY_DIR}
)

set_property(TARGET bot PROPERTY CXX_STANDARD 23)

get_target_property(grpc_cpp_plugin_location gRPC::grpc_cpp_plugin LOCATION)
protobuf_generate(TARGET bot LANGUAGE cpp)
protobuf_generate(TARGET bot LANGUAGE grpc GENERATE_EXTENSIONS .grpc.pb.h .grpc.pb.cc PLUGIN "protoc-gen-grpc=${grpc_cpp_plugin_location}")
