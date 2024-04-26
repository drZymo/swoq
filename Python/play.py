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
    
    def __init__(self, quest:bool=False, plot:bool=True, print:bool=True):
        self.quest = quest
        self.plot = plot
        self.print = print
        
        self.channel = grpc.insecure_channel('localhost:5009')
        if quest:
            self.stub = swoq_pb2_grpc.QuestStub(self.channel)
        else:
            self.stub = swoq_pb2_grpc.TrainingStub(self.channel)
   

    def close(self) -> None:
        self.channel.close()
        
    
    def __enter__(self) -> object:
        return self

    
    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.close()
        

    def start(self, level:int=0) -> None:
        global played_id
        
        if self.quest:        
            startResponse = self.stub.Start(swoq_pb2.StartQuestRequest(playerId=player_id))
        else:
            startResponse = self.stub.Start(swoq_pb2.StartTrainingRequest(playerId=player_id, level=level))

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
        
        if self.plot:
            self._frame = plot_map(self.map)
    

    def update_global_state(self, state:swoq_pb2.State) -> None:
        self.level = state.level
        self.finished = state.finished
        self.player_pos = (state.player1.position.y, state.player1.position.x)
        self.inventory = state.player1.inventory
        self.has_sword = state.player1.hasSword
        if self.print:
            print(f'finished={self.finished} level={self.level}, player_pos={self.player_pos}, inventory={self.inventory}, has_sword={self.has_sword}')
        
        # Clear map for every new level
        if self.prev_level != self.level:
            self.prev_level = self.level
            self.map = np.zeros_like(self.map)
            print(f'Entered level {self.level}')

        if len(state.player1.surroundings) > 0:
            top = self.player_pos[0] - self.visibility_range
            left = self.player_pos[1] - self.visibility_range
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

        self.player_distances, self.player_paths = compute_distances(self.map, self.player_pos)
        self.player_distances_nokeys, self.player_paths_nokeys = compute_distances(self.map, self.player_pos, exclude_cells={ KEY_RED, KEY_GREEN, KEY_BLUE })


    def act(self, action:swoq_pb2.Action, direction:swoq_pb2.Direction) -> bool:
        response = self.stub.Act(swoq_pb2.ActionRequest(gameId=self.game_id, action1=action, direction1=direction))

        if response.result == 0:
            self.update_global_state(response.state)

        if self.plot:
            update_map(self._frame, self.map)
            
        if self.print:
            result = _result_strings[response.result]
            print(f'{result=}')
            print(f' finished={self.finished}')
            
        return response.result == 0


    def move(self, direction:str) -> bool:
        global to_swoq_pb2_direction
        dir = to_swoq_pb2_direction[direction]
        return self.act(swoq_pb2.MOVE, dir)
        

    def use(self, direction:str) -> bool:
        global to_swoq_pb2_direction
        dir = to_swoq_pb2_direction[direction]
        return self.act(swoq_pb2.USE, dir)


    def get_direction_from_player_towards(self, to_pos: tuple[int,int]) -> str:
        if self.player_pos == to_pos: return None
        
        paths = self.player_paths_nokeys
        
        # prevent picking up keys accidentally unless it is the target key
        excluded_cells = {KEY_RED, KEY_GREEN, KEY_BLUE}
        target_type = self.map[to_pos[0], to_pos[1]]
        if target_type in excluded_cells:
            paths = self.player_paths

        return get_direction_towards(paths, self.player_pos, to_pos)


    def get_direction_towards_closest_unknown(self) -> str:
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
                if pos in self.player_distances_nokeys:
                    dist = self.player_distances_nokeys[pos]
                    if closest_dist is None or dist < closest_dist:
                        closest_dist = dist
                        closest_empty = pos
                
        if closest_empty is None:
            return None

        return get_direction_towards(self.player_paths_nokeys, self.player_pos, closest_empty)


    def explore(self) -> None:
        while True:
            # stop immediately when exit is visible
            if np.any(self.map == EXIT): break
            # stop immediately to pickup sword
            if not self.has_sword and np.any(self.map == SWORD): break
            # stop immediately to pickup health
            if np.any(self.map == HEALTH): break

            # stop immediately if matching key and door have been found
            if np.any(self.map == KEY_RED) and np.any(self.map == DOOR_RED): break
            if np.any(self.map == KEY_GREEN) and np.any(self.map == DOOR_GREEN): break
            if np.any(self.map == KEY_BLUE) and np.any(self.map == DOOR_BLUE): break
            if self.inventory == INVENTORY_KEY_RED and np.any(self.map == DOOR_RED): break
            if self.inventory == INVENTORY_KEY_GREEN and np.any(self.map == DOOR_GREEN): break
            if self.inventory == INVENTORY_KEY_BLUE and np.any(self.map == DOOR_BLUE): break

            direction = self.get_direction_towards_closest_unknown()
            if direction is None: break
            if not self.move(direction): break
            #sleep(0.05)


    def try_reach_exit(self) -> None:
        exit_pos = np.argwhere(self.map == EXIT)
        if not np.any(exit_pos): return
        
        exit_pos = exit_pos[0]
        while True:
            direction = self.get_direction_from_player_towards(tuple(exit_pos))
            if direction is None: break
            if not self.move(direction): break


    def move_to_target(self, target_pos:tuple[int,int]) -> None:
        while True:
            direction = self.get_direction_from_player_towards(target_pos)
            if direction is None: break
            if not self.move(direction): break


    def try_get_key(self) ->None:
        if self.inventory != INVENTORY_NONE: return

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

    
    def try_get_sword(self) -> None:
        if self.has_sword: return
        
        target_pos = None
        
        swords = np.argwhere(self.map == SWORD)
        if np.any(swords):
            target_pos = tuple(swords[0])

        if target_pos is not None:
            self.move_to_target(target_pos)


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
        
        if target_pos is None and self.inventory == INVENTORY_KEY_RED:
            doors = np.argwhere(self.map == DOOR_RED)
            if np.any(doors): find_target(doors[0])

        if target_pos is None and self.inventory == INVENTORY_KEY_GREEN:
            doors = np.argwhere(self.map == DOOR_GREEN)
            if np.any(doors): find_target(doors[0])

        if target_pos is None and self.inventory == INVENTORY_KEY_BLUE:
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
        if self.map[self.player_pos[0]-1, self.player_pos[1]] == ENEMY: self.use('N')
        elif self.map[self.player_pos[0]+1, self.player_pos[1]] == ENEMY: self.use('S')
        elif self.map[self.player_pos[0], self.player_pos[1]-1] == ENEMY: self.use('W')
        elif self.map[self.player_pos[0], self.player_pos[1]+1] == ENEMY: self.use('E')
        else:
            target_pos = np.argwhere(self.map == ENEMY)
            if len(target_pos) > 0:
                self.move_to_target(tuple(target_pos[0]))
