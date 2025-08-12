# SWOQ Bot starter - Python version

This is a starter kit to write your own bot for the Sioux Weekend of Quest, using Python.

# Getting started

Install the necessary packages, generate the gRPC code, configure your environment and you are ready to go.

## Install requirements

The bot basically only requires a Python 3.12 environment and the `grpcio-tools` packages, which can be installed using conda or pip. It was tested with Python 3.12.9 and grpcio-tools 1.62.2 (conda) and 1.70.0 (pip).

### Using conda

Make sure you have a Anaconda environment installed, like [Miniconda](https://www.anaconda.com/docs/getting-started/miniconda/main).

- Open your Anaconda command prompt.
- Create a new environment: `conda create -n swoq python=3.12.9 grpcio-tools=1.62.2 python-dotenv=0.21.0`
- Activate: `conda activate swoq`

### Using pip

If you already have a working Python 3.12 environment, then you can use the following command to install the necessary packages.

    pip install -r requirements.txt

## Generate gRPC code

The `grpcio` library uses generated files that contain the data types and classes to communicate with a gRPC service. These can be generated using `grpcio-tools` with the following command:

    python -m grpc_tools.protoc --python_out=. --pyi_out=. --grpc_python_out=. --proto_path=. swoq.proto

Everytime you update the `swoq.proto` file, you have to regenerate these files.

## Configure environment

The bot uses a `.env` file to store the configuration of the environment. See `example.env` for what it should contain. Update it accordingly.

# Running

The entry point (main) is in `bot.py`. And you can simply run it with:

    python bot.py

The file `swoq.py` contains all the logic for communicating with the server and storing a replay file.

## Replay files

Every interaction with the server is logged in a replay file. The standard implementation saves them as `.swoq` files in the `Replays` subfolder. These files can be viewed with the **ReplayViewer** application distributed separately.
