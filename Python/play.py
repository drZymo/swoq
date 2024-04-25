import grpc
import swoq_pb2
import swoq_pb2_grpc
import numpy as np
from map_util import *


player_id = '6616b1c5bd0a697480a68319'


def update_global_state(state):
    global map, visibility_range
    global player_pos, inventory, has_sword, finished

    player_pos = (state.player1.position.y, state.player1.position.x)
    inventory = state.player1.inventory
    has_sword = state.player1.hasSword
    finished = state.finished
    print(f'{player_pos=}, {inventory=}, {finished=}')

    if len(state.player1.surroundings) > 0:
        top = player_pos[0] - visibility_range
        left = player_pos[1] - visibility_range
        i = 0
        for y in range(visibility_range*2 + 1):
            for x in range(visibility_range*2 + 1):
                s = state.player1.surroundings[i]
                if s != UNKNOWN:
                    map_x = left + x
                    map_y = top + y
                    if 0 <= map_y < map.shape[0] and 0 <= map_x < map.shape[1]:
                        map[map_y, map_x] = s
                i += 1


def start(level:int=0, quest:bool=False):
    global map, game_id, visibility_range, is_quest
    
    is_quest = quest
    
    with grpc.insecure_channel('localhost:5009') as channel:
        if is_quest:
            stub = swoq_pb2_grpc.QuestStub(channel)
            startResponse = stub.Start(swoq_pb2.StartQuestRequest(playerId=player_id))
        else:
            stub = swoq_pb2_grpc.TrainingStub(channel)
            startResponse = stub.Start(swoq_pb2.StartTrainingRequest(playerId=player_id, level=level))

    print(f'{startResponse.result=}')

    game_id = startResponse.gameId
    height = startResponse.height
    width = startResponse.width
    visibility_range = startResponse.visibilityRange
    print(f'{game_id=}, {height=}, {width=}, {visibility_range=}')

    map = np.zeros((height, width), dtype=np.int8)

    update_global_state(startResponse.state)


to_swoq_pb2_direction = {
    'N': swoq_pb2.NORTH,
    'E': swoq_pb2.EAST,
    'S': swoq_pb2.SOUTH,
    'W': swoq_pb2.WEST,
    None: None,
}
def move(direction: str) -> bool:
    global frame, to_swoq_pb2_direction, is_quest
    
    dir = to_swoq_pb2_direction[direction]
    
    with grpc.insecure_channel('localhost:5009') as channel:
        if is_quest:
            stub = swoq_pb2_grpc.QuestStub(channel)
        else:
            stub = swoq_pb2_grpc.TrainingStub(channel)
        moveResponse = stub.Act(swoq_pb2.ActionRequest(gameId=game_id, action1=swoq_pb2.MOVE, direction1=dir))

    if moveResponse.result == 0:
        update_global_state(moveResponse.state)

    update_map(frame, map)
    print(f'{moveResponse.result=}')
    if moveResponse.result == 0:
        print(f' finished={moveResponse.state.finished}')
    return moveResponse.result == 0
    
def use(direction: str) -> bool:
    global frame, to_swoq_pb2_direction, is_quest

    dir = to_swoq_pb2_direction[direction]

    with grpc.insecure_channel('localhost:5009') as channel:
        if is_quest:
            stub = swoq_pb2_grpc.QuestStub(channel)
        else:
            stub = swoq_pb2_grpc.TrainingStub(channel)
        useResponse = stub.Act(swoq_pb2.ActionRequest(gameId=game_id, action1=swoq_pb2.USE, direction1=dir))

    if useResponse.result == 0:
        update_global_state(useResponse.state)
        
    update_map(frame, map)
    print(f'{useResponse.result=}')
    if useResponse.result == 0:
        print(f' finished={useResponse.state.finished}')
    return useResponse.result == 0


def softmax(a):
    e = np.exp(a)
    return e / np.sum(e)

def pick_random_move(map):
    height, width = map.shape
    player_pos = np.unravel_index(np.argwhere(map == 2).flatten()[0], (height, width))
    target_pos = np.array([height-2, width-1])
    print(f'{player_pos=}, {target_pos=}')

    diff = target_pos - player_pos
    direction = np.sign(diff)

    # increase probability for actions in right direction
    moves = np.ones((4,))
    if direction[0] < 0: moves[0] += 1 # NORTH
    if direction[0] > 0: moves[1] += 1 # SOUTH
    if direction[1] < 0: moves[2] += 1 # WEST
    if direction[1] > 0: moves[3] += 1 # EAST
    print(f'{moves=}')
    moves = softmax(moves)

    # choose random move
    directions = [swoq_pb2.NORTH, swoq_pb2.SOUTH, swoq_pb2.WEST, swoq_pb2.EAST]
    direction = np.random.choice(directions, p=moves)
    print(f'{direction=}')
    return direction


def get_direction_towards_closest_unknown():
    global map, player_pos

    distances, paths = compute_distances(map, player_pos, exclude_cells={ KEY_RED, KEY_GREEN, KEY_BLUE })

    closest_empty = None
    closest_dist = None

    # find closest empty with UNKNOWN neighbors
    for pos in np.argwhere(map == EMPTY):
        pos = tuple(pos)
        pos_y, pos_x = pos
        if map[pos_y, pos_x-1] == UNKNOWN or \
                map[pos_y, pos_x+1] == UNKNOWN or \
                map[pos_y+1, pos_x] == UNKNOWN or \
                map[pos_y-1, pos_x] == UNKNOWN:
            if pos in distances:
                dist = distances[pos]
                if closest_dist is None or dist < closest_dist:
                    closest_dist = dist
                    closest_empty = pos
               
    if closest_empty is None:
        return None

    return get_direction_towards(paths, player_pos, closest_empty)


def explore():
    while True:
        # stop immediately when exit is visible
        exit_pos = np.argwhere(map == EXIT)
        if np.any(exit_pos): break

        direction = get_direction_towards_closest_unknown()
        if direction is None: break
        if not move(direction): break
        #sleep(0.05)



def try_reach_exit():
    global map
    
    exit_pos = np.argwhere(map == EXIT)
    if not np.any(exit_pos): return
    
    exit_pos = exit_pos[0]
    while True:
        direction = get_direction_from_towards(map, player_pos, tuple(exit_pos))
        if direction is None: break
        if not move(direction): break



def move_to_target(target_pos: tuple[int, int]) -> None:
    global map, player_pos
    while True:
        direction = get_direction_from_towards(map, player_pos, target_pos)
        if direction is None: break
        if not move(direction): break


def try_get_key():
    global map, inventory
    
    if inventory != INVENTORY_NONE: return

    target_pos = None
    
    doors = np.argwhere(map == DOOR_RED)
    keys = np.argwhere(map == KEY_RED)
    if target_pos is None and np.any(doors) and np.any(keys):
        target_pos = tuple(keys[0])

    doors = np.argwhere(map == DOOR_GREEN)
    keys = np.argwhere(map == KEY_GREEN)
    if target_pos is None and np.any(doors) and np.any(keys):
        target_pos = tuple(keys[0])

    doors = np.argwhere(map == DOOR_BLUE)
    keys = np.argwhere(map == KEY_BLUE)
    if target_pos is None and np.any(doors) and np.any(keys):
        target_pos = tuple(keys[0])

    if target_pos is not None:
        move_to_target(target_pos)



def try_get_sword():
    global map, has_sword
    
    if has_sword: return
    
    target_pos = None
    
    swords = np.argwhere(map == SWORD)
    if np.any(swords):
        target_pos = tuple(swords[0])

    if target_pos is not None:
        move_to_target(target_pos)



def try_open_door():
    global map, inventory
    
    target_pos = None
    use_direction = None
    
    def find_target(door):
        nonlocal target_pos, use_direction
        global map
        
        top = (door[0]-1, door[1])
        if target_pos is None and map[top[0], top[1]] == EMPTY:
            target_pos = top
            use_direction = 'S'
        bottom = (door[0]+1, door[1])
        if target_pos is None and map[bottom[0], bottom[1]] == EMPTY:
            target_pos = bottom
            use_direction = 'N'
        left = (door[0], door[1]-1)
        if target_pos is None and map[left[0], left[1]] == EMPTY:
            target_pos = left
            use_direction = 'E'
        right = (door[0], door[1]+1)
        if target_pos is None and map[right[0], right[1]] == EMPTY:
            target_pos = right
            use_direction = 'W'
    
    if target_pos is None and inventory == INVENTORY_KEY_RED:
        doors = np.argwhere(map == DOOR_RED)
        if np.any(doors): find_target(doors[0])

    if target_pos is None and inventory == INVENTORY_KEY_GREEN:
        doors = np.argwhere(map == DOOR_GREEN)
        if np.any(doors): find_target(doors[0])

    if target_pos is None and inventory == INVENTORY_KEY_BLUE:
        doors = np.argwhere(map == DOOR_BLUE)
        if np.any(doors): find_target(doors[0])
                
    if target_pos is not None:
        move_to_target(target_pos)
        use(use_direction)
            


def move_to_pressure_plate():
    global map

    target_pos = np.argwhere(map == PRESSURE_PLATE)
    if len(target_pos) > 0:
        move_to_target(tuple(target_pos[0]))


def fight_enemy():
    global map

    if map[player_pos[0]-1, player_pos[1]] == ENEMY: use('N')
    elif map[player_pos[0]+1, player_pos[1]] == ENEMY: use('S')
    elif map[player_pos[0], player_pos[1]-1] == ENEMY: use('W')
    elif map[player_pos[0], player_pos[1]+1] == ENEMY: use('E')
    else:
        target_pos = np.argwhere(map == ENEMY)
        if len(target_pos) > 0:
            move_to_target(tuple(target_pos[0]))
