from game_service import GameService
import swoq_pb2

user_id = '<insert your user id here>'
level = 0 # None for quest, 0.. (i.e. integer) for train

def main() -> None:
    with GameService(user_id) as game:
        move_east = True
        state = game.start(level)
        while not state.finished:
            action = swoq_pb2.DIRECTEDACTION_MOVE_EAST if move_east else swoq_pb2.DIRECTEDACTION_MOVE_SOUTH
            state = game.act(action)
            move_east = not move_east

if __name__ == '__main__':
    main()
