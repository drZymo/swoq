## Generate gRPC stubs

    python -m grpc_tools.protoc -I. --python_out=. --pyi_out=. --grpc_python_out=. --proto_path=. swoq.proto