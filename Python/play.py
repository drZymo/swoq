import grpc
import swoq_pb2
import swoq_pb2_grpc
import numpy as np
from map_util import *


player_id = '6616b1c5bd0a697480a68319'

to_swoq_pb2_direction = {
    'N': swoq_pb2.NORTH,
    'E': swoq_pb2.EAST,
    'S': swoq_pb2.SOUTH,
    'W': swoq_pb2.WEST,
    None: None,
}


_result_strings  = {
    swoq_pb2.OK: 'OK',
    swoq_pb2.INTERNAL_ERROR: 'INTERNAL_ERROR',
    swoq_pb2.PLAYER_ALREADY_REGISTERED: 'PLAYER_ALREADY_REGISTERED',
    swoq_pb2.UNKNOWN_PLAYER: 'UNKNOWN_PLAYER',
    swoq_pb2.UNKNOWN_GAME_ID: 'UNKNOWN_GAME_ID',
    swoq_pb2.LEVEL_NOT_AVAILABLE: 'LEVEL_NOT_AVAILABLE',
    swoq_pb2.MOVE_NOT_ALLOWED: 'MOVE_NOT_ALLOWED',
    swoq_pb2.USE_NOT_ALLOWED: 'USE_NOT_ALLOWED',
    swoq_pb2.UNKNOWN_ACTION: 'UNKNOWN_ACTION',
    swoq_pb2.UNKNOWN_DIRECTION: 'UNKNOWN_DIRECTION',
    swoq_pb2.GAME_FINISHED: 'GAME_FINISHED',
    swoq_pb2.PLAYER1_NOT_PRESENT: 'PLAYER1_NOT_PRESENT',
    swoq_pb2.PLAYER2_NOT_PRESENT: 'PLAYER2_NOT_PRESENT',
    swoq_pb2.INVENTORY_FULL: 'INVENTORY_FULL',
    swoq_pb2.INVENTORY_EMPTY: 'INVENTORY_EMPTY',
    swoq_pb2.NO_SWORD: 'NO_SWORD',
    swoq_pb2.PLAYER1_DIED: 'PLAYER1_DIED',
    swoq_pb2.PLAYER2_DIED: 'PLAYER2_DIED',
    swoq_pb2.UNKNOWN_QUEST_ID: 'UNKNOWN_QUEST_ID',
}

class GamePlayer:

    def __init__(self, plot:bool=True, print:bool=True):
        self.plot = plot
        self.print = print

        self.channel = grpc.insecure_channel('localhost:5009')
        self.stub = swoq_pb2_grpc.GameServiceStub(self.channel)


    def close(self) -> None:
        self.channel.close()


    def __enter__(self) -> object:
        return self


    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.close()


    def start(self, level:int=None) -> None:
        global played_id

        startResponse = self.stub.Start(swoq_pb2.StartRequest(playerId=player_id, level=level))

        if self.print:
            result = _result_strings[startResponse.result]
            print(f'{result=}')

        self.game_id = startResponse.gameId
        self.height = startResponse.height
        self.width = startResponse.width
        self.visibility_range = startResponse.visibilityRange

        self.prev_level = -1
        self.map = np.zeros((self.height, self.width), dtype=np.int8)

        self.update_global_state(startResponse.state)

        self.action1:swoq_pb2.Action = None
        self.direction1:swoq_pb2.Direction = None
        self.action2:swoq_pb2.Action = None
        self.direction2:swoq_pb2.Direction = None

        if self.plot:
            self._frame = plot_map(self.map)


    def update_global_state(self, state:swoq_pb2.State) -> None:
        self.level = state.level
        self.finished = state.finished

        self.player1_pos = (state.player1.position.y, state.player1.position.x) if state.HasField("player1") else None
        self.player1_health = state.player1.health if state.HasField("player1") else None
        self.player1_inventory = state.player1.inventory if state.HasField("player1") else None
        self.player1_has_sword = state.player1.hasSword if state.HasField("player1") else None

        self.player2_pos = (state.player2.position.y, state.player2.position.x) if state.HasField("player2") else None
        self.player2_health = state.player2.health if state.HasField("player2") else None
        self.player2_inventory = state.player2.inventory if state.HasField("player2") else None
        self.player2_has_sword = state.player2.hasSword if state.HasField("player2") else None

        if self.print:
            print(f'finished={self.finished} level={self.level}')
            print(f'player1: pos={self.player1_pos}, health={self.player1_health}, inventory={self.player1_inventory}, has_sword={self.player1_has_sword}')
            print(f'player2: pos={self.player2_pos}, health={self.player2_health}, inventory={self.player2_inventory}, has_sword={self.player2_has_sword}')

        # Clear map for every new level
        if self.prev_level != self.level:
            self.prev_level = self.level
            self.map = np.zeros_like(self.map)
            print(f'Entered level {self.level}')

        if len(state.player1.surroundings) > 0:
            top = self.player1_pos[0] - self.visibility_range
            left = self.player1_pos[1] - self.visibility_range
            i = 0
            for y in range(self.visibility_range*2 + 1):
                for x in range(self.visibility_range*2 + 1):
                    s = state.player1.surroundings[i]
                    if s != UNKNOWN:
                        map_x = left + x
                        map_y = top + y
                        if 0 <= map_y < self.map.shape[0] and 0 <= map_x < self.map.shape[1]:
                            self.map[map_y, map_x] = s
                    i += 1

        if len(state.player2.surroundings) > 0:
            top = self.player2_pos[0] - self.visibility_range
            left = self.player2_pos[1] - self.visibility_range
            i = 0
            for y in range(self.visibility_range*2 + 1):
                for x in range(self.visibility_range*2 + 1):
                    s = state.player2.surroundings[i]
                    if s != UNKNOWN:
                        map_x = left + x
                        map_y = top + y
                        if 0 <= map_y < self.map.shape[0] and 0 <= map_x < self.map.shape[1]:
                            self.map[map_y, map_x] = s
                    i += 1

        # Remove all players from map
        self.map[self.map == PLAYER] = EMPTY
        if self.player1_pos is not None:
            y,x = self.player1_pos
            if y >= 0 and x >=0: self.map[y,x] = PLAYER
        if self.player2_pos is not None:
            y,x = self.player2_pos
            if y >= 0 and x >=0: self.map[y,x] = PLAYER

        if self.player1_pos is not None:
            self.player1_distances, self.player1_paths = compute_distances_quick(self.map, self.player1_pos)

        if self.player2_pos is not None:
            self.player2_distances, self.player2_paths = compute_distances_quick(self.map, self.player2_pos)


    def act(self):
        print(f'{self.action1=}, {self.action2=}')
        response = self.stub.Act(swoq_pb2.ActionRequest(gameId=self.game_id, action1=self.action1, direction1=self.direction1, action2=self.action2, direction2=self.direction2))

        self.update_global_state(response.state)

        if self.plot:
            update_map(self._frame, self.map)

        if self.print:
            result = _result_strings[response.result]
            print(f'{result=}')
            print(f' finished={self.finished}')

        # clear for next act
        self.action1:swoq_pb2.Action = None
        self.direction1:swoq_pb2.Direction = None
        self.action2:swoq_pb2.Action = None
        self.direction2:swoq_pb2.Direction = None

        return response.result == 0


    def move(self, direction:str) -> bool:
        global to_swoq_pb2_direction
        dir = to_swoq_pb2_direction[direction]
        self.action1 = swoq_pb2.MOVE
        self.direction1 = dir
        return self.act()


    def use(self, direction:str) -> bool:
        global to_swoq_pb2_direction
        dir = to_swoq_pb2_direction[direction]
        self.action1 = swoq_pb2.USE
        self.direction1 = dir
        return self.act()


    def queue_move1(self, direction:str) -> None:
        global to_swoq_pb2_direction
        assert(self.action1 is None)
        self.action1 = swoq_pb2.MOVE
        self.direction1 = to_swoq_pb2_direction[direction]


    def queue_move2(self, direction:str) -> None:
        global to_swoq_pb2_direction
        assert(self.action2 is None)
        self.action2 = swoq_pb2.MOVE
        self.direction2 = to_swoq_pb2_direction[direction]


    def queue_use1(self, direction:str) -> None:
        global to_swoq_pb2_direction
        assert(self.action1 is None)
        self.action1 = swoq_pb2.USE
        self.direction1 = to_swoq_pb2_direction[direction]


    def queue_use2(self, direction:str) -> None:
        global to_swoq_pb2_direction
        assert(self.action2 is None)
        self.action2 = swoq_pb2.USE
        self.direction2 = to_swoq_pb2_direction[direction]


    def step(self) -> None:
        if self.finished: return

        self.try_explore1()
        self.try_explore2()
        # self.explore()
        self.try_reach_exit1()
        self.try_reach_exit2()
        # if self.finished: return
        # self.try_get_health()
        self.try_get_sword1()
        self.try_get_sword2()
        # self.fight_enemy()
        # self.try_get_key()
        # self.try_open_door()
        # self.move_to_pressure_plate()

        self.act()


    def try_explore1(self) -> None:
        if self.action1 is not None: return

        # stop immediately when exit is visible
        if np.any(self.map == EXIT): return
        # stop immediately to pickup sword
        if not self.player1_has_sword and np.any(self.map == SWORD): return
        # stop immediately to pickup health
        if np.any(self.map == HEALTH): return

        # stop immediately if matching key and door have been found
        if np.any(self.map == KEY_RED) and np.any(self.map == DOOR_RED): return
        if np.any(self.map == KEY_GREEN) and np.any(self.map == DOOR_GREEN): return
        if np.any(self.map == KEY_BLUE) and np.any(self.map == DOOR_BLUE): return
        if self.player1_inventory == INVENTORY_KEY_RED and np.any(self.map == DOOR_RED): return
        if self.player1_inventory == INVENTORY_KEY_GREEN and np.any(self.map == DOOR_GREEN): return
        if self.player1_inventory == INVENTORY_KEY_BLUE and np.any(self.map == DOOR_BLUE): return

        direction = self.get_direction_towards_closest_unknown(self.player1_pos, self.player1_distances_nokeys, self.player1_paths_nokeys)
        if direction is None: return

        self.queue_move1(direction)


    def try_explore2(self) -> None:
        if self.action2 is not None: return

        # stop immediately when exit is visible
        if np.any(self.map == EXIT): return
        # stop immediately to pickup sword
        if not self.player2_has_sword and np.any(self.map == SWORD): return
        # stop immediately to pickup health
        if np.any(self.map == HEALTH): return

        # stop immediately if matching key and door have been found
        if np.any(self.map == KEY_RED) and np.any(self.map == DOOR_RED): return
        if np.any(self.map == KEY_GREEN) and np.any(self.map == DOOR_GREEN): return
        if np.any(self.map == KEY_BLUE) and np.any(self.map == DOOR_BLUE): return
        if self.player2_inventory == INVENTORY_KEY_RED and np.any(self.map == DOOR_RED): return
        if self.player2_inventory == INVENTORY_KEY_GREEN and np.any(self.map == DOOR_GREEN): return
        if self.player2_inventory == INVENTORY_KEY_BLUE and np.any(self.map == DOOR_BLUE): return

        direction = self.get_direction_towards_closest_unknown(self.player2_pos, self.player2_distances_nokeys, self.player2_paths_nokeys)
        if direction is None: return

        self.queue_move2(direction)


    def get_direction_from_to(self, from_pos:tuple[int,int], to_pos:tuple[int,int], paths, paths_nokeys) -> str:
        if from_pos == to_pos: return None

        current_paths = paths_nokeys

        # prevent picking up keys accidentally unless it is the target key
        excluded_cells = {KEY_RED, KEY_GREEN, KEY_BLUE}
        target_type = self.map[to_pos[0], to_pos[1]]
        if target_type in excluded_cells:
            current_paths = paths

        return get_direction_towards(current_paths, from_pos, to_pos)


    def get_direction_towards_closest_unknown(self, from_pos, distances, paths) -> str:
        closest_empty = None
        closest_dist = None

        # find closest empty with UNKNOWN neighbors
        for pos in np.argwhere(self.map == EMPTY):
            pos = tuple(pos)
            pos_y, pos_x = pos
            if self.map[pos_y, pos_x-1] == UNKNOWN or \
                    self.map[pos_y, pos_x+1] == UNKNOWN or \
                    self.map[pos_y+1, pos_x] == UNKNOWN or \
                    self.map[pos_y-1, pos_x] == UNKNOWN:
                if pos in distances:
                    dist = distances[pos]
                    if closest_dist is None or dist < closest_dist:
                        closest_dist = dist
                        closest_empty = pos

        if closest_empty is None:
            return None

        return get_direction_towards(paths, from_pos, closest_empty)


    def try_reach_exit1(self) -> None:
        if self.action1 is not None: return

        exit_pos = np.argwhere(self.map == EXIT)
        if not np.any(exit_pos): return

        exit_pos = exit_pos[0]
        direction = self.get_direction_from_to(self.player1_pos, tuple(exit_pos), self.player1_paths, self.player1_paths_nokeys)
        if direction is None: return

        self.queue_move1(direction)


    def try_reach_exit2(self) -> None:
        if self.action2 is not None: return

        exit_pos = np.argwhere(self.map == EXIT)
        if not np.any(exit_pos): return

        exit_pos = exit_pos[0]
        direction = self.get_direction_from_to(self.player2_pos, tuple(exit_pos), self.player2_paths, self.player2_paths_nokeys)
        if direction is None: return

        self.queue_move2(direction)


    def move_to_target(self, target_pos:tuple[int,int]) -> None:
        while True:
            direction = self.get_direction_from_player_towards(target_pos)
            if direction is None: break
            if not self.move(direction): break


    def try_get_key(self) ->None:
        if self.player1_inventory != INVENTORY_NONE: return

        target_pos = None

        doors = np.argwhere(self.map == DOOR_RED)
        keys = np.argwhere(self.map == KEY_RED)
        if target_pos is None and np.any(doors) and np.any(keys):
            target_pos = tuple(keys[0])

        doors = np.argwhere(self.map == DOOR_GREEN)
        keys = np.argwhere(self.map == KEY_GREEN)
        if target_pos is None and np.any(doors) and np.any(keys):
            target_pos = tuple(keys[0])

        doors = np.argwhere(self.map == DOOR_BLUE)
        keys = np.argwhere(self.map == KEY_BLUE)
        if target_pos is None and np.any(doors) and np.any(keys):
            target_pos = tuple(keys[0])

        if target_pos is not None:
            self.move_to_target(target_pos)


    def try_get_sword1(self) -> None:
        if self.action1 is not None: return
        if self.player1_has_sword: return

        swords = np.argwhere(self.map == SWORD)
        if not np.any(swords): return
        target_pos = tuple(swords[0])

        direction = self.get_direction_from_to(self.player1_pos, target_pos, self.player1_paths, self.player1_paths_nokeys)
        if direction is None: return

        self.queue_move1(direction)


    def try_get_sword2(self) -> None:
        if self.action2 is not None: return
        if self.player2_has_sword: return

        swords = np.argwhere(self.map == SWORD)
        if not np.any(swords): return
        target_pos = tuple(swords[0])

        direction = self.get_direction_from_to(self.player2_pos, target_pos, self.player2_paths, self.player2_paths_nokeys)
        if direction is None: return

        self.queue_move2(direction)


    def try_get_health(self) -> None:
        target_pos = None

        healths = np.argwhere(self.map == HEALTH)
        if np.any(healths):
            target_pos = tuple(healths[0])

        if target_pos is not None:
            self.move_to_target(target_pos)


    def try_open_door(self) -> None:
        target_pos = None
        use_direction = None

        def find_target(door):
            nonlocal target_pos, use_direction

            top = (door[0]-1, door[1])
            if target_pos is None and self.map[top[0], top[1]] == EMPTY:
                target_pos = top
                use_direction = 'S'
            bottom = (door[0]+1, door[1])
            if target_pos is None and self.map[bottom[0], bottom[1]] == EMPTY:
                target_pos = bottom
                use_direction = 'N'
            left = (door[0], door[1]-1)
            if target_pos is None and self.map[left[0], left[1]] == EMPTY:
                target_pos = left
                use_direction = 'E'
            right = (door[0], door[1]+1)
            if target_pos is None and self.map[right[0], right[1]] == EMPTY:
                target_pos = right
                use_direction = 'W'

        if target_pos is None and self.player1_inventory == INVENTORY_KEY_RED:
            doors = np.argwhere(self.map == DOOR_RED)
            if np.any(doors): find_target(doors[0])

        if target_pos is None and self.player1_inventory == INVENTORY_KEY_GREEN:
            doors = np.argwhere(self.map == DOOR_GREEN)
            if np.any(doors): find_target(doors[0])

        if target_pos is None and self.player1_inventory == INVENTORY_KEY_BLUE:
            doors = np.argwhere(self.map == DOOR_BLUE)
            if np.any(doors): find_target(doors[0])

        if target_pos is not None:
            self.move_to_target(target_pos)
            self.use(use_direction)


    def move_to_pressure_plate(self) -> None:
        target_pos = np.argwhere(self.map == PRESSURE_PLATE)
        if len(target_pos) > 0:
            self.move_to_target(tuple(target_pos[0]))


    def fight_enemy(self) -> None:
        if self.map[self.player1_pos[0]-1, self.player1_pos[1]] == ENEMY: self.use('N')
        elif self.map[self.player1_pos[0]+1, self.player1_pos[1]] == ENEMY: self.use('S')
        elif self.map[self.player1_pos[0], self.player1_pos[1]-1] == ENEMY: self.use('W')
        elif self.map[self.player1_pos[0], self.player1_pos[1]+1] == ENEMY: self.use('E')
        else:
            target_pos = np.argwhere(self.map == ENEMY)
            if len(target_pos) > 0:
                self.move_to_target(tuple(target_pos[0]))
