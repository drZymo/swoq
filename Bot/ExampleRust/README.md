# SWOQ Bot starter - Rust version

This is a starter kit to write your own bot for the Sioux Weekend of Quest, using Rust.

## Getting started

- Make sure you have Rust installed (use [rustup](https://www.rust-lang.org/tools/install))
- Download and install `protoc` for your platform from [protobuf (GitHub)](https://github.com/protocolbuffers/protobuf/releases).
- Set the environment variable `PROTOC` to the path of the `protoc` executable, e.g.:

      PROTOC=C:\Tools\protoc-31.1-win64\bin\protoc.exe

- Compile with `cargo build`.
- Copy `example.env` to `.env` and edit its contents.
- Run with `cargo run`.

## Development

gRPC client and messages are automatically generated during build (see `build.rs`)

The entry-point to the bot is `main.rs`.

Happy coding!

### VSCode

For VSCode there are some tasks pre-defined for building and debugging.
Make sure you have the extensions "rust-analyzer" and "CodeLLDB" installed.

## License

MIT
