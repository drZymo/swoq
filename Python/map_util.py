import numpy as np
import matplotlib.pyplot as plt
from IPython.display import clear_output, display

UNKNOWN = 0
EMPTY = 1
PLAYER = 2
WALL = 3
EXIT = 4
DOOR_RED = 5
KEY_RED = 6
DOOR_GREEN = 7
KEY_GREEN = 8
DOOR_BLUE = 9
KEY_BLUE = 10

INVENTORY_NONE = 0
INVENTORY_KEY_RED = 1
INVENTORY_KEY_GREEN = 2
INVENTORY_KEY_BLUE = 3

_cell_colors = {
    UNKNOWN:    [  0,   0,   0],
    EMPTY:      [ 50,  50,  50],
    PLAYER:     [255, 255, 255],
    WALL:       [147, 124,  93],
    EXIT:       [230, 217, 177],
    DOOR_RED:   [128,   0,   0],
    KEY_RED:    [255,   0,   0],
    DOOR_GREEN: [  0, 128,  0],
    KEY_GREEN:  [  0, 255,   0],
    DOOR_BLUE:  [  0,   0, 128],
    KEY_BLUE:   [  0,   0, 255],
}

def get_map_image(map: np.ndarray[np.int8]) -> np.ndarray[np.float32]:
    height, width = map.shape
    map_img = np.zeros((height, width, 3), dtype=np.float32)
    for y in range(height):
        for x in range(width):
            map_img[y, x] = (np.array(_cell_colors[map[y, x]]) / 255).astype(np.float32)
    return map_img


def plot_map(map):
    map_img = get_map_image(map)
    fig = plt.figure()
    plt.gca().set_axis_off()
    plt.tight_layout(pad=0)
    img = plt.imshow(map_img)
    plt.show()
    return (fig, img)

def update_map(frame, map):
    map_img = get_map_image(map)

    fig, img = frame
    img.set_data(map_img)
    clear_output(wait=True)
    display(fig)
    
    
def is_wall(map: np.ndarray[np.int8], pos: tuple[int,int]) -> bool:
    cell = map[pos[0], pos[1]]
    return cell == WALL or cell == DOOR_RED or cell == DOOR_GREEN or cell == DOOR_BLUE or cell == UNKNOWN

def compute_distances(map: np.ndarray[np.int8], from_pos: tuple[int,int]) -> tuple[dict, dict]:
    height, width = map.shape
    
    todo = []
    distances = {}
    paths = {}

    todo.append(from_pos)
    distances[from_pos] = 0

    def enqueue(cur_pos, cur_dist, next_pos):
        nonlocal map, distances, todo, paths
        if not is_wall(map, next_pos):
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

def get_direction_from_towards(map: np.ndarray[np.int8], from_pos: tuple[int,int], to_pos: tuple[int,int]) -> str:
    if from_pos == to_pos: return None
    _, paths = compute_distances(map, from_pos)
    return get_direction_towards(paths, from_pos, to_pos)




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
