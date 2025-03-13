from swoq import GameConnection
import swoq_pb2

level = 0 # None for quest, 0.. (i.e. integer) for train

def main() -> None:
    with GameConnection() as connection:
        with connection.start(level) as game:
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
