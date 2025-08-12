from swoq import GameConnection
from os import getenv
from dotenv import load_dotenv
import swoq_pb2

def str_to_int(s:(str|None)) -> (int|None):
    return None if s is None else int(s)

def main() -> None:
    load_dotenv()
    
    with GameConnection(getenv('SWOQ_USER_ID'), getenv('SWOQ_USER_NAME'), getenv('SWOQ_HOST'), getenv('SWOQ_REPLAYS_FOLDER')) as connection:
        with connection.start(str_to_int(getenv('SWOQ_LEVEL')), str_to_int(getenv('SWOQ_SEED'))) as game:
            print(f'Game {game.game_id} started')
            if game.seed is not None: print(f'- seed: {game.seed}')
            print(f'- map size: {game.map_height}x{game.map_width}')

            move_east = True
            while game.state.status == swoq_pb2.GAME_STATUS_ACTIVE:
                action = swoq_pb2.DIRECTED_ACTION_MOVE_EAST if move_east else swoq_pb2.DIRECTED_ACTION_MOVE_SOUTH
                print(f'tick: {game.state.tick}, action: {swoq_pb2.DirectedAction.Name(action)}')
                game.act(action)
                move_east = not move_east

if __name__ == '__main__':
    main()
