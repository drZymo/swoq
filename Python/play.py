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

class GamePlayer:
    
    def __init__(self, quest:bool=False):
        self.quest = quest
        
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

        print(f'{startResponse.result=}')

        self.game_id = startResponse.gameId
        self.height = startResponse.height
        self.width = startResponse.width
        self.visibility_range = startResponse.visibilityRange

        self.level = level
        self.map = np.zeros((self.height, self.width), dtype=np.int8)

        self.update_global_state(startResponse.state)
        
        self._frame = plot_map(self.map)
    

    def update_global_state(self, state:swoq_pb2.State) -> None:
        self.player_pos = (state.player1.position.y, state.player1.position.x)
        self.inventory = state.player1.inventory
        self.has_sword = state.player1.hasSword
        self.finished = state.finished
        print(f'{self.player_pos=}, {self.inventory=}, {self.finished=}')
        
        # Clear map for every new level
        if state.level != self.level:
            self.level = state.level
            self.map = np.zeros_like(self.map)

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


    def act(self, action:swoq_pb2.Action, direction:swoq_pb2.Direction) -> bool:
        response = self.stub.Act(swoq_pb2.ActionRequest(gameId=self.game_id, action1=action, direction1=direction))

        if response.result == 0:
            self.update_global_state(response.state)

        update_map(self._frame, self.map)
        print(f'{response.result=}')
        if response.result == 0:
            print(f' finished={response.state.finished}')
        return response.result == 0


    def move(self, direction:str) -> bool:
        global to_swoq_pb2_direction
        dir = to_swoq_pb2_direction[direction]
        return self.act(swoq_pb2.MOVE, dir)
        

    def use(self, direction:str) -> bool:
        global to_swoq_pb2_direction
        dir = to_swoq_pb2_direction[direction]
        return self.act(swoq_pb2.USE, dir)


    def get_direction_towards_closest_unknown(self) -> str:
        distances, paths = compute_distances(self.map, self.player_pos, exclude_cells={ KEY_RED, KEY_GREEN, KEY_BLUE })

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

        return get_direction_towards(paths, self.player_pos, closest_empty)


    def explore(self) -> None:
        while True:
            # stop immediately when exit is visible
            if np.any(self.map == EXIT): break
            
            # stop immediately if sword is found
            if not self.has_sword and np.any(self.map == SWORD): break

            # stop immediately if matching key and door have been found
            if np.any(self.map == KEY_RED) and np.any(self.map == DOOR_RED): break
            if np.any(self.map == KEY_GREEN) and np.any(self.map == DOOR_GREEN): break
            if np.any(self.map == KEY_BLUE) and np.any(self.map == DOOR_BLUE): break

            direction = self.get_direction_towards_closest_unknown()
            if direction is None: break
            if not self.move(direction): break
            #sleep(0.05)


    def try_reach_exit(self) -> None:
        exit_pos = np.argwhere(self.map == EXIT)
        if not np.any(exit_pos): return
        
        exit_pos = exit_pos[0]
        while True:
            direction = get_direction_from_towards(self.map, self.player_pos, tuple(exit_pos))
            if direction is None: break
            if not self.move(direction): break


    def move_to_target(self, target_pos:tuple[int,int]) -> None:
        while True:
            direction = get_direction_from_towards(self.map, self.player_pos, target_pos)
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
