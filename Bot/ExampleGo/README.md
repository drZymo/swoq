# SWOQ Bot starter - Go version

This is a starter kit to write your own bot for the Sioux Weekend of Quest, using Go.

## Getting started

- Make sure you have Go installed (see the [manual](https://go.dev/doc/install))
- Download and install `protoc` for your platform from [protobuf (GitHub)](https://github.com/protocolbuffers/protobuf/releases).
- Make sure that both `go` and `protoc` are added to your `PATH`.
- Install the protobuf plugins for Go:

        go install google.golang.org/protobuf/cmd/protoc-gen-go@latest
        go install google.golang.org/grpc/cmd/protoc-gen-go-grpc@latest

- Generate the `.go` files from the `.proto` file with the following command. You have to do that every time the proto file changes!

        protoc --go_out=Mproto/swoq.proto=proto/swoq:. --go-grpc_out=Mproto/swoq.proto=proto/swoq:. proto/swoq.proto

  - If `protoc` can't find the plugin, ensure they're in your path, or use the `--plugin` option to specify their location. E.g.:

        --plugin=$HOME/go/bin/protoc-gen-go --plugin=$HOME/go/bin/protoc-gen-go-grpc

- Copy `example.env` to `.env` and edit its contents.
- Compile and run with `go run bot`.

## Development

The entry-point to the bot is `main.go`.

Happy coding!

## License

MIT
