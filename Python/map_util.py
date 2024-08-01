import numpy as np
import matplotlib.pyplot as plt
from IPython.display import clear_output, display
import swoq_pb2

_cell_colors = {
    swoq_pb2.TILE_UNKNOWN:              [  0,   0,   0],
    swoq_pb2.TILE_EMPTY:                [ 64,  64,  64],
    swoq_pb2.TILE_PLAYER:               [255,   0, 255],
    swoq_pb2.TILE_WALL:                 [147, 124,  93],
    swoq_pb2.TILE_EXIT:                 [230, 217, 177],
    swoq_pb2.TILE_DOOR_RED:             [128,   0,   0],
    swoq_pb2.TILE_KEY_RED:              [255,   0,   0],
    swoq_pb2.TILE_DOOR_GREEN:           [  0, 128,   0],
    swoq_pb2.TILE_KEY_GREEN:            [  0, 255,   0],
    swoq_pb2.TILE_DOOR_BLUE:            [  0,   0, 128],
    swoq_pb2.TILE_KEY_BLUE:             [  0,   0, 255],
    swoq_pb2.TILE_BOULDER:              [ 49,  41,  31],
    swoq_pb2.TILE_PRESSURE_PLATE_RED:   [ 64,  32,  32],
    swoq_pb2.TILE_PRESSURE_PLATE_GREEN: [ 32,  64,  32],
    swoq_pb2.TILE_PRESSURE_PLATE_BLUE:  [ 32,  32,  64],
    swoq_pb2.TILE_SWORD:                [255, 255,   0],
    swoq_pb2.TILE_HEALTH:               [128, 128,   0],
    swoq_pb2.TILE_ENEMY:                [  0, 192, 192],
    swoq_pb2.TILE_BOSS:                 [  0, 255, 255],
    swoq_pb2.TILE_TREASURE:             [255, 255, 255],
}

def get_map_image(game_map: np.ndarray[np.int8]) -> np.ndarray[np.float32]:
    height, width = game_map.shape
    map_img = np.zeros((height, width, 3), dtype=np.float32)
    for y in range(height):
        for x in range(width):
            map_img[y, x] = (np.array(_cell_colors[game_map[y, x]]) / 255).astype(np.float32)
    return map_img


def plot_map(game_map):
    map_img = get_map_image(game_map)
    fig = plt.figure()
    plt.gca().set_axis_off()
    plt.tight_layout(pad=0)
    img = plt.imshow(map_img)
    plt.show()
    return (fig, img)

def update_map(frame, game_map):
    map_img = get_map_image(game_map)

    fig, img = frame
    img.set_data(map_img)
    clear_output(wait=True)
    display(fig)


def is_wall(game_map: np.ndarray[np.int8], pos: tuple[int,int]) -> bool:
    cell = game_map[pos[0], pos[1]]
    return cell == swoq_pb2.TILE_WALL or \
        cell == swoq_pb2.TILE_DOOR_RED or \
        cell == swoq_pb2.TILE_DOOR_GREEN or \
        cell == swoq_pb2.TILE_DOOR_BLUE or \
        cell == swoq_pb2.TILE_BOULDER or \
        cell == swoq_pb2.TILE_UNKNOWN


def compute_distances(game_map: np.ndarray[np.int8], from_pos: tuple[int,int], exclude_cells: set[int]=None) -> tuple[dict, dict]:
    height, width = game_map.shape

    todo = []
    distances = {}
    paths = {}

    todo.append(from_pos)
    distances[from_pos] = 0

    def is_excluded(pos):
        nonlocal game_map, exclude_cells
        if exclude_cells is None: return False
        return game_map[pos[0],pos[1]] in exclude_cells

    def enqueue(cur_pos, cur_dist, next_pos):
        nonlocal game_map, distances, todo, paths
        if not is_wall(game_map, next_pos) and not is_excluded(next_pos):
            next_dist = distances[next_pos] if next_pos in distances else np.inf
            if cur_dist + 1 < next_dist:
                distances[next_pos] = cur_dist + 1
                paths[next_pos] = cur_pos
                todo.append(next_pos)

    while todo:
        cur_y, cur_x = cur_pos = todo[0]
        cur_dist = distances[cur_pos]
        del todo[0]

        if cur_y > 0: enqueue(cur_pos, cur_dist, (cur_y-1, cur_x))
        if cur_y < height-1: enqueue(cur_pos, cur_dist, (cur_y+1, cur_x))
        if cur_x > 0: enqueue(cur_pos, cur_dist, (cur_y, cur_x-1))
        if cur_x < width-1: enqueue(cur_pos, cur_dist, (cur_y, cur_x+1))

    return distances, paths


def compute_distances_quick(game_map: np.ndarray[np.int8], from_pos: tuple[int,int]) -> tuple[dict, dict]:
    height, width = game_map.shape

    todo = []
    distances = {}
    paths = {}

    todo.append(from_pos)
    distances[from_pos] = 0

    def enqueue(cur_pos, cur_dist, next_pos):
        nonlocal game_map, distances, todo, paths
        if next_pos not in distances and game_map[next_pos[0], next_pos[1]] == swoq_pb2.TILE_EMPTY:
            distances[next_pos] = cur_dist + 1
            paths[next_pos] = cur_pos
            todo.append(next_pos)

    while todo:
        cur_y, cur_x = cur_pos = todo[0]
        cur_dist = distances[cur_pos]
        del todo[0]

        if cur_y > 0: enqueue(cur_pos, cur_dist, (cur_y-1, cur_x))
        if cur_y < height-1: enqueue(cur_pos, cur_dist, (cur_y+1, cur_x))
        if cur_x > 0: enqueue(cur_pos, cur_dist, (cur_y, cur_x-1))
        if cur_x < width-1: enqueue(cur_pos, cur_dist, (cur_y, cur_x+1))

    return distances, paths


def get_direction_towards(paths: dict, from_pos: tuple[int,int], to_pos: tuple[int,int]) -> str:
    if to_pos not in paths: return None

    # Get first step in path towards target
    cur = to_pos
    prev = None
    while cur != from_pos:
        prev = cur
        cur = paths[cur]
    next_pos = prev
    assert(next_pos is not None)

    diff_y = next_pos[0] - from_pos[0]
    diff_x = next_pos[1] - from_pos[1]
    if diff_y > 0: return 'S'
    if diff_y < 0: return 'N'
    if diff_x > 0: return 'E'
    if diff_x < 0: return 'W'

    return None


def get_direction(from_pos, to_pos, distances, paths):
    if to_pos in distances:
        return get_direction_towards(paths, from_pos, to_pos)

    north = (to_pos[0]-1, to_pos[1])
    south = (to_pos[0]+1, to_pos[1])
    west = (to_pos[0], to_pos[1]-1)
    east = (to_pos[0], to_pos[1]+1)

    if from_pos == north: return 'S'
    if from_pos == south: return 'N'
    if from_pos == west: return 'E'
    if from_pos == east: return 'W'

    min_dist = np.inf
    adjacent_pos = None
    if north in distances and distances[north] < min_dist:
        min_dist = distances[north]
        adjacent_pos = north
    if south in distances and distances[south] < min_dist:
        min_dist = distances[south]
        adjacent_pos = south
    if east in distances and distances[east] < min_dist:
        min_dist = distances[east]
        adjacent_pos = east
    if west in distances and distances[west] < min_dist:
        min_dist = distances[west]
        adjacent_pos = west

    return get_direction_towards(paths, from_pos, adjacent_pos)


def plot_distances(distances: dict[tuple,int], height: int, width: int) -> None:
    D = np.zeros((height, width), np.float32)
    for y in range(height):
        for x in range(width):
            p = (y,x)
            if p in distances:
                D[y,x] = distances[p]
            else:
                D[y,x] = np.inf

    plt.figure()
    plt.imshow(D)


def get_adjacent_pos(pos, distances):
    top = (pos[0]-1, pos[1])
    bottom = (pos[0]+1, pos[1])
    left = (pos[0], pos[1]-1)
    right = (pos[0], pos[1]+1)
    min_dist = np.inf
    min_pos = None
    if top in distances and distances[top] < min_dist:
        min_dist = distances[top]
        min_pos = top
    if bottom in distances:
        min_dist = distances[bottom]
        min_pos = bottom
    if left in distances:
        min_dist = distances[left]
        min_pos = left
    if right in distances:
        min_dist = distances[right]
        min_pos = right
    return min_pos


def are_adjacent(pos1, pos2):
    dy = pos2[0] - pos1[0]
    dx = pos2[1] - pos1[1]
    return (abs(dy) == 1 and dx == 0) or (dy == 0 and abs(dx) == 1)


def valid_pos(pos:tuple[int,int]) -> bool:
    return pos is not None and pos[0] >= 0 and pos[1] >= 0


def get_dist_to_other_player(player1_pos:tuple[int,int], player2_pos:tuple[int,int], player1_distances) -> int|None:
    if not valid_pos(player1_pos): return None
    if not valid_pos(player2_pos): return None

    if are_adjacent(player1_pos, player2_pos): return 1

    adj_pos = get_adjacent_pos(player2_pos, player1_distances)
    if adj_pos is None: return None

    dist = player1_distances[adj_pos] + 1
    return dist
