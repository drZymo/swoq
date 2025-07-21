
# SWOQ Bot starter - C++ version

This is a starter kit to write your own bot for the Sioux Weekend of Quest, using C++.

This example code uses C++23 features (like `std::expected` and `std::println`), so a compatible compiler is required. It has been tested with GCC version 14.2 on Ubuntu 24.04.

## Getting started

gRPC is used, but for C++ there are no usable packages available. You will have to compile gRPC manually. See the [gRPC C++ Quick start](https://grpc.io/docs/languages/cpp/quickstart/) for more information. This has only been tested on Ubuntu 24.04, not on Windows.

Once gRPC is installed, then the following steps will get your Ubuntu environment ready for development of your own bot.

- Install CMake and GCC 14

      sudo apt install cmake build-essential gcc-14 g++-14

- Clone this repo and prepare a build folder with CMake

      cmake -S . -B build -DCMAKE_C_COMPILER=gcc-14 -DCMAKE_CXX_COMPILER=g++-14

- Build the bot

      cmake --build build --config Release

- Copy `example.env` to `.env` and edit its contents.
- Run the bot

      build/bot

Happy coding!

## Tips

When using Windows, you could use WSL to create an Ubuntu environment en use VSCode remote support to use this environment for compilation.

## Licence

MIT
