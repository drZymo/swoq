import grpc
import swoq_pb2
import swoq_pb2_grpc
import numpy as np
from map_util import *
from time import sleep

to_swoq_pb2_action = {
    'MN': swoq_pb2.DIRECTEDACTION_MOVE_NORTH,
    'ME': swoq_pb2.DIRECTEDACTION_MOVE_EAST,
    'MS': swoq_pb2.DIRECTEDACTION_MOVE_SOUTH,
    'MW': swoq_pb2.DIRECTEDACTION_MOVE_WEST,
    'UN': swoq_pb2.DIRECTEDACTION_USE_NORTH,
    'UE': swoq_pb2.DIRECTEDACTION_USE_EAST,
    'US': swoq_pb2.DIRECTEDACTION_USE_SOUTH,
    'UW': swoq_pb2.DIRECTEDACTION_USE_WEST,
    None: None,
}

_result_strings  = {
    swoq_pb2.RESULT_OK: 'OK',

    swoq_pb2.RESULT_INTERNAL_ERROR: 'INTERNAL_ERROR',
    swoq_pb2.RESULT_UNKNOWN_USER: 'UNKNOWN_USER',
    swoq_pb2.RESULT_UNKNOWN_GAME_ID: 'UNKNOWN_GAME_ID',
    swoq_pb2.RESULT_USER_LEVEL_TOO_LOW: 'USER_LEVEL_TOO_LOW',
    swoq_pb2.RESULT_QUEST_QUEUED: 'QUEST_QUEUED',
    swoq_pb2.RESULT_MOVE_NOT_ALLOWED: 'MOVE_NOT_ALLOWED',
    swoq_pb2.RESULT_NO_PROGRESS: 'NO_PROGRESS',
    swoq_pb2.RESULT_UNKNOWN_ACTION: 'UNKNOWN_ACTION',
    swoq_pb2.RESULT_GAME_FINISHED: 'GAME_FINISHED',
    swoq_pb2.RESULT_PLAYER1_NOT_PRESENT: 'PLAYER1_NOT_PRESENT',
    swoq_pb2.RESULT_USE_NOT_ALLOWED: 'USE_NOT_ALLOWED',
    swoq_pb2.RESULT_INVENTORY_FULL: 'INVENTORY_FULL',
    swoq_pb2.RESULT_INVENTORY_EMPTY: 'INVENTORY_EMPTY',
    swoq_pb2.RESULT_PLAYER1_DIED: 'PLAYER1_DIED',
    swoq_pb2.RESULT_NO_SWORD: 'NO_SWORD',
    swoq_pb2.RESULT_PLAYER2_NOT_PRESENT: 'PLAYER2_NOT_PRESENT',
    swoq_pb2.RESULT_PLAYER2_DIED: 'PLAYER2_DIED',
}

class GamePlayer:

    def __init__(self, user_id:str, plot:bool=True, print:bool=True):
        self.user_id = user_id
        self.plot = plot
        self.print = print

        self.remain_on_plate_counter = 0
        self.plate_color = None

        self.channel = grpc.insecure_channel('localhost:5009')
        self.stub = swoq_pb2_grpc.GameServiceStub(self.channel)


    def close(self) -> None:
        self.channel.close()


    def __enter__(self) -> object:
        return self


    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.close()


    def start(self, level:int=None) -> None:
        startResponse = self.stub.Start(swoq_pb2.StartRequest(userId=self.user_id, level=level))
        if self.print:
            result = _result_strings[startResponse.result]
            print(f'{result=}')

        while startResponse.result == swoq_pb2.RESULT_QUEST_QUEUED:
            sleep(1)
            startResponse = self.stub.Start(swoq_pb2.StartRequest(userId=self.user_id, level=level))
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

        self.action1:swoq_pb2.DirectedAction = None
        self.action2:swoq_pb2.DirectedAction = None
        
        self.reset()

        if self.plot:
            self._frame = plot_map(self.map)


    def reset(self) -> None:
        self.prev_level = self.level
        self.map = np.zeros_like(self.map)
        self.expected_enemy_health = None
        self.plates_with_boulders = []
        self.boulder_drop_pos_1 = None
        self.boulder_drop_pos_2 = None
        self.remain_on_plate_counter_1 = 0
        self.plate_color_1 = None
        self.plate_pos_1 = None
        self.remain_on_plate_counter_2 = 0
        self.plate_color_2 = None
        self.plate_pos_2 = None
        self.random_pos1 = None
        self.random_pos2 = None

    def update_global_state(self, state:swoq_pb2.State) -> None:
        self.level = state.level
        self.finished = state.finished

        self.combined_health = 0
        
        self.player1_pos = (state.player1.position.y, state.player1.position.x) if state.HasField("player1") else None
        self.player1_health = state.player1.health if state.HasField("player1") else None
        self.player1_inventory = state.player1.inventory if state.HasField("player1") else None
        self.player1_has_sword = state.player1.hasSword if state.HasField("player1") else None
        if self.player1_health is not None:
            self.combined_health += self.player1_health

        self.player2_pos = (state.player2.position.y, state.player2.position.x) if state.HasField("player2") else None
        self.player2_health = state.player2.health if state.HasField("player2") else None
        self.player2_inventory = state.player2.inventory if state.HasField("player2") else None
        self.player2_has_sword = state.player2.hasSword if state.HasField("player2") else None
        if self.player2_health is not None:
            self.combined_health += self.player2_health
        
        self.two_players = valid_pos(self.player1_pos) and valid_pos(self.player2_pos)

        if self.print:
            print(f'finished={self.finished} level={self.level}, comb health={self.combined_health}')
            print(f'player1: pos={self.player1_pos}, health={self.player1_health}, inventory={self.player1_inventory}, has_sword={self.player1_has_sword}')
            print(f'player2: pos={self.player2_pos}, health={self.player2_health}, inventory={self.player2_inventory}, has_sword={self.player2_has_sword}')

        # Clear map for every new level
        if self.prev_level != self.level:
            print(f'Entered level {self.level}')
            self.reset()

        # Copy surroundings to map
        if len(state.player1.surroundings) > 0:
            top = self.player1_pos[0] - self.visibility_range
            left = self.player1_pos[1] - self.visibility_range
            i = 0
            for y in range(self.visibility_range*2 + 1):
                for x in range(self.visibility_range*2 + 1):
                    s = state.player1.surroundings[i]
                    if s != swoq_pb2.TILE_UNKNOWN:
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
                    if s != swoq_pb2.TILE_UNKNOWN:
                        map_x = left + x
                        map_y = top + y
                        if 0 <= map_y < self.map.shape[0] and 0 <= map_x < self.map.shape[1]:
                            self.map[map_y, map_x] = s
                    i += 1

        # Manually place players, to prevent lingering entries
        self.map[self.map == swoq_pb2.TILE_PLAYER] = swoq_pb2.TILE_EMPTY
        if self.player1_pos is not None:
            y,x = self.player1_pos
            if y >= 0 and x >=0: self.map[y,x] = swoq_pb2.TILE_PLAYER
        if self.player2_pos is not None:
            y,x = self.player2_pos
            if y >= 0 and x >=0: self.map[y,x] = swoq_pb2.TILE_PLAYER

        # Update paths
        if self.player1_pos is not None:
            self.player1_distances, self.player1_paths = compute_distances_quick(self.map, self.player1_pos)

        if self.player2_pos is not None:
            self.player2_distances, self.player2_paths = compute_distances_quick(self.map, self.player2_pos)
            

    def act(self):
        # Once in a while player 2 will not move, to break cycles
        if np.random.uniform() < 0.05:
            print(f' player 2 skipped')
            self.action2 = None

        print(f'{self.action1=}, {self.action2=}')
        response = self.stub.Act(swoq_pb2.ActionRequest(gameId=self.game_id, action1=self.action1, action2=self.action2))

        self.update_global_state(response.state)

        if self.plot:
            update_map(self._frame, self.map)

        if self.print:
            result = _result_strings[response.result]
            print(f'{result=}')
            print(f' finished={self.finished}')

        # clear for next act
        self.action1:swoq_pb2.DirectedAction = None
        self.action2:swoq_pb2.DirectedAction = None

        return response.result == 0


    def queue_move1(self, direction:str) -> None:
        global to_swoq_pb2_action
        assert(self.action1 is None)
        if direction is not None:
            self.action1 = to_swoq_pb2_action['M'+direction]


    def queue_move2(self, direction:str) -> None:
        global to_swoq_pb2_action
        assert(self.action2 is None)
        if direction is not None:
            self.action2 = to_swoq_pb2_action['M'+direction]


    def queue_use1(self, direction:str) -> None:
        global to_swoq_pb2_action
        assert(self.action1 is None)
        if direction is not None:
            self.action1 = to_swoq_pb2_action['U'+direction]


    def queue_use2(self, direction:str) -> None:
        global to_swoq_pb2_action
        assert(self.action2 is None)
        if direction is not None:
            self.action2 = to_swoq_pb2_action['U'+direction]


    def step(self) -> None:
        if self.finished: return

        self.plate_pos_2 = None
        self.plate_pos_1 = None

        if self.level == 18:
            self.handle_level18()
        self.move_to_exit()
        self.pickup_health()
        self.pickup_sword()
        self.pickup_keys()
        self.attack()
        self.explore()
        if self.level != 18:
            self.move_to_pressure_plate()
            self.wait_at_pressure_plate_door_2()
        self.pickup_boulder()
        if self.level != 18:
            self.wait_at_random_door()
        self.random_walk() # fallback
        self.act()
        self.update_remain_on_plate()


    def get_direction_towards_closest_unknown(self, from_pos, distances, paths) -> str:
        closest_empty = None
        closest_dist = None
        
        # find closest empty with UNKNOWN neighbors
        for pos_y, pos_x in np.argwhere(self.map == swoq_pb2.TILE_UNKNOWN):
            pos = None
            if (pos_y, pos_x-1) in distances and self.map[pos_y, pos_x-1] == swoq_pb2.TILE_EMPTY:
                pos = (pos_y, pos_x-1)
            elif (pos_y, pos_x+1) in distances and self.map[pos_y, pos_x+1] == swoq_pb2.TILE_EMPTY:
                pos = (pos_y, pos_x+1)
            elif (pos_y-1, pos_x) in distances and self.map[pos_y-1, pos_x] == swoq_pb2.TILE_EMPTY:
                pos = (pos_y-1, pos_x)
            elif (pos_y+1, pos_x) in distances and self.map[pos_y+1, pos_x] == swoq_pb2.TILE_EMPTY:
                pos = (pos_y+1, pos_x)

            if pos is not None:
                dist = distances[pos]
                if closest_dist is None or dist < closest_dist:
                    closest_dist = dist
                    closest_empty = pos

        if closest_empty is None:
            return None

        return get_direction_towards(paths, from_pos, closest_empty)


    def can_act1(self) -> bool:
        return valid_pos(self.player1_pos) and self.action1 is None and self.remain_on_plate_counter_1 <= 0


    def can_act2(self) -> bool:
        return valid_pos(self.player2_pos) and self.action2 is None and self.remain_on_plate_counter_2 <= 0


    def move_to_1(self, pos:tuple[int,int]) -> None:
        dir = get_direction(self.player1_pos, pos, self.player1_distances, self.player1_paths)
        self.queue_move1(dir)


    def move_to_2(self, pos:tuple[int,int]) -> None:
        dir = get_direction(self.player2_pos, pos, self.player2_distances, self.player2_paths)
        self.queue_move2(dir)


    def use_1(self, pos:tuple[int,int]) -> None:
        if self.player1_pos[0] < pos[0]: self.queue_use1('S')
        if self.player1_pos[0] > pos[0]: self.queue_use1('N')
        if self.player1_pos[1] < pos[1]: self.queue_use1('E')
        if self.player1_pos[1] > pos[1]: self.queue_use1('W')


    def use_2(self, pos:tuple[int,int]) -> None:
        if self.player2_pos[0] < pos[0]: self.queue_use2('S')
        if self.player2_pos[0] > pos[0]: self.queue_use2('N')
        if self.player2_pos[1] < pos[1]: self.queue_use2('E')
        if self.player2_pos[1] > pos[1]: self.queue_use2('W')


    def pickup_key_or_open_door(self, key:int, door:int, item:int) -> None:
        doors = np.argwhere(self.map == door)
        if np.any(doors):
            
            # open door
            if self.can_act1() and self.player1_inventory == item:
                self.use_closest_1(doors, 'door')

            if self.can_act2() and self.player2_inventory == item:
                self.use_closest_2(doors, 'door')

            # pick up keys for doors visible
            keys = np.argwhere(self.map == key)
            if np.any(keys):
                if self.can_act1() and self.player1_inventory == 0:
                    self.move_to_closest_1(keys, 'key')

                if self.can_act2() and self.player2_inventory == 0:
                    self.move_to_closest_2(keys, 'key')

    
    def move_to_exit(self) -> None:
        # Move to exit if possible
        exits = np.argwhere(self.map == swoq_pb2.TILE_EXIT)
        if np.any(exits):
            exit_pos = tuple(exits[0])

            # Only move when both players can reach the exit (or each other)
            can_reach = False
            if valid_pos(self.player1_pos) and valid_pos(self.player2_pos):
                can_reach = (self.can_1_reach(exit_pos) and self.can_2_reach(self.player1_pos)) or \
                    (self.can_1_reach(self.player2_pos) and self.can_2_reach(exit_pos))
            elif valid_pos(self.player1_pos):
                can_reach = self.can_1_reach(exit_pos)
            elif valid_pos(self.player2_pos):
                can_reach = self.can_2_reach(exit_pos)

            if can_reach:
                if self.can_act1():
                    # drop boulder before entering exit
                    if self.player1_inventory == swoq_pb2.INVENTORY_BOULDER:
                        if self.boulder_drop_pos_1 is None:
                            self.boulder_drop_pos_1 = self.get_closest_clearing(self.player1_pos, self.player1_distances, self.player1_paths)
                        if self.boulder_drop_pos_1 is not None:
                            if are_adjacent(self.player1_pos, self.boulder_drop_pos_1):
                                print('boulder_drop1')
                                self.use_1(self.boulder_drop_pos_1)
                                self.boulder_drop_pos_1 = None
                            else:
                                print('boulder_drop_move1')
                                self.move_to_1(self.boulder_drop_pos_1)
                    else:
                        print('exit1')
                        self.move_to_1(exit_pos)
                if self.can_act2():
                    # drop boulder before entering exit
                    if self.player2_inventory == swoq_pb2.INVENTORY_BOULDER:
                        if self.boulder_drop_pos_2 is None:
                            self.boulder_drop_pos_2 = self.get_closest_clearing(self.player2_pos, self.player2_distances, self.player2_paths)
                        if self.boulder_drop_pos_2 is not None:
                            if are_adjacent(self.player2_pos, self.boulder_drop_pos_2):
                                print('boulder_drop2')
                                self.use_2(self.boulder_drop_pos_2)
                                self.boulder_drop_pos_2 = None
                            else:
                                print('boulder_drop_move2')
                                self.move_to_2(self.boulder_drop_pos_2)
                    else:
                        print('exit2')
                        self.move_to_2(exit_pos)


    def get_closest_clearing(self, from_pos, distances, paths) -> tuple[str, tuple]:
        closest_empty = None
        closest_dist = None
        
        # find closest empty with EMPTY neighbors
        for pos_y, pos_x in np.argwhere(self.map == swoq_pb2.TILE_EMPTY):
            if (pos_y, pos_x-1) in distances and self.map[pos_y, pos_x-1] == swoq_pb2.TILE_EMPTY and \
               (pos_y, pos_x+1) in distances and self.map[pos_y, pos_x+1] == swoq_pb2.TILE_EMPTY and \
               (pos_y-1, pos_x) in distances and self.map[pos_y-1, pos_x] == swoq_pb2.TILE_EMPTY and \
               (pos_y+1, pos_x) in distances and self.map[pos_y+1, pos_x] == swoq_pb2.TILE_EMPTY:
                pos = (pos_y, pos_x)
                dist = distances[pos]
                if closest_dist is None or dist < closest_dist:
                    closest_dist = dist
                    closest_empty = pos

        return closest_empty


    def attack(self) -> None:
        # Attack
        enemies = np.argwhere(self.map == swoq_pb2.TILE_ENEMY)
        if np.any(enemies):
            if self.expected_enemy_health is None:
                self.expected_enemy_health = 6

            can_attack_1 = self.player1_has_sword and self.player1_health > 1
            can_attack_2 = self.player2_has_sword and self.player2_health > 1

            # If both can still attack, then coordinate by moving closer together
            if can_attack_1 and can_attack_2:
                # make sure players are close to each other so they can both attack
                dist_1_to_2 = get_dist_to_other_player(self.player1_pos, self.player2_pos, self.player1_distances) if self.player1_pos is not None else None
                dist_2_to_1 = get_dist_to_other_player(self.player2_pos, self.player1_pos, self.player2_distances) if self.player2_pos is not None else None
                if dist_1_to_2 is None and dist_2_to_1 is None:
                    dist_players = None
                elif dist_1_to_2 is None:
                    dist_players = dist_2_to_1
                elif dist_2_to_1 is None:
                    dist_players = dist_1_to_2
                else:
                    dist_players = min(dist_1_to_2, dist_2_to_1)

                if self.can_act1() and dist_players is not None and dist_players > 10:
                    print('move_closer_1')
                    self.move_to_1(self.player2_pos)
                if self.can_act2() and dist_players is not None and dist_players > 4:
                    print('move_closer_2')
                    self.move_to_2(self.player1_pos)

            if self.can_act1() and can_attack_1:
                self.use_closest_1(enemies, 'attack')

            if self.can_act2() and can_attack_2:
                self.use_closest_2(enemies, 'attack')

            if self.expected_enemy_health is not None and self.expected_enemy_health <= 0:
                self.expected_enemy_health = None


    def pickup_health(self) -> None:
        # Pickup health
        healths = np.argwhere(self.map == swoq_pb2.TILE_HEALTH)
        if np.any(healths):
            # let player 1 pickup health first
            if self.can_act1() and (self.player2_health is None or self.player1_health <= self.player2_health):
                self.move_to_closest_1(healths, 'health')
            elif self.can_act2() and (self.player1_health is None or self.player1_health > self.player2_health):
                self.move_to_closest_2(healths, 'health')


    def pickup_sword(self) -> None:
        # Pickup sword
        swords = np.argwhere(self.map == swoq_pb2.TILE_SWORD)
        if np.any(swords):
            if self.can_act1() and not self.player1_has_sword:
                self.move_to_closest_1(swords, 'sword1')

            # let player 1 pickup sword first
            if self.can_act2() and not self.player2_has_sword and self.player1_has_sword:
                self.move_to_closest_2(swords, 'sword2')


    def pickup_keys(self) -> None:
        # Pickup keys
        self.pickup_key_or_open_door(swoq_pb2.TILE_KEY_RED, swoq_pb2.TILE_DOOR_RED, swoq_pb2.INVENTORY_KEY_RED)
        self.pickup_key_or_open_door(swoq_pb2.TILE_KEY_GREEN, swoq_pb2.TILE_DOOR_GREEN, swoq_pb2.INVENTORY_KEY_GREEN)
        self.pickup_key_or_open_door(swoq_pb2.TILE_KEY_BLUE, swoq_pb2.TILE_DOOR_BLUE, swoq_pb2.INVENTORY_KEY_BLUE)


    def explore(self) -> None:
        # Explore
        if self.can_act1():
            dir = self.get_direction_towards_closest_unknown(self.player1_pos, self.player1_distances, self.player1_paths)
            if dir is not None:
                print('explore1')
                self.queue_move1(dir)
        if self.can_act2():
            dir = self.get_direction_towards_closest_unknown(self.player2_pos, self.player2_distances, self.player2_paths)
            if dir is not None:
                print('explore2')
                self.queue_move2(dir)


    def random_walk(self) -> None:
        if self.random_pos1 is not None and self.player1_pos == self.random_pos1:
            self.random_pos1 = None
        if self.random_pos2 is not None and self.player2_pos == self.random_pos2:
            self.random_pos2 = None
            
        if self.can_act1():
            if self.random_pos1 is None or not self.can_1_reach(self.random_pos1):
                positions = list(self.player1_distances.keys())
                positions.remove(self.player1_pos) # not own pos
                self.random_pos1 = positions[np.random.choice(len(positions))]
            if self.random_pos1 is not None:
                print(f'random1 {self.random_pos1}')
                self.move_to_1(self.random_pos1)
        if self.can_act2():
            if self.random_pos2 is None or not self.can_2_reach(self.random_pos2):
                positions = list(self.player2_distances.keys())
                positions.remove(self.player2_pos) # not own pos
                self.random_pos2 = positions[np.random.choice(len(positions))]
            if self.random_pos2 is not None:
                print(f'random2 {self.random_pos2}')
                self.move_to_2(self.random_pos2)
            

    def move_to_pressure_plate(self) -> None:
        plates = np.argwhere((self.map == swoq_pb2.TILE_PRESSURE_PLATE_RED) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_GREEN) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_BLUE))
        if np.any(plates):
            if self.can_act1():
                if self.player1_inventory == 4:
                    # place boulders on plates
                    plate_pos = self.use_closest_1(plates, 'plate_boulder')
                    if plate_pos is not None:
                        self.plate_pos_1 = plate_pos
                        self.plate_color_1 = self.map[self.plate_pos_1]
                        self.plates_with_boulders.append(self.plate_pos_1)
                elif self.two_players:
                    # only player 1 will stand on plates with two players
                    plate_pos = self.move_to_closest_1(plates, 'plate')
                    if plate_pos is not None:
                        self.plate_pos_1 = plate_pos
                        self.plate_color_1 = self.map[self.plate_pos_1]
            
            if self.can_act2() and self.player2_inventory == 4:
                # place boulders on plates
                plate_pos = self.use_closest_2(plates, 'plate_boulder')
                if plate_pos is not None:
                    self.plate_pos_2 = plate_pos
                    self.plate_color_2 = self.map[self.plate_pos_2]
                    self.plates_with_boulders.append(self.plate_pos_2)


    def wait_at_pressure_plate_door_2(self) -> None:
        if self.plate_color_1 == swoq_pb2.TILE_PRESSURE_PLATE_RED:
            plate_doors = np.argwhere(self.map == swoq_pb2.TILE_DOOR_RED)
        elif self.plate_color_1  == swoq_pb2.TILE_PRESSURE_PLATE_GREEN:
            plate_doors = np.argwhere(self.map == swoq_pb2.TILE_DOOR_GREEN)
        elif self.plate_color_1  == swoq_pb2.TILE_PRESSURE_PLATE_BLUE:
            plate_doors = np.argwhere(self.map == swoq_pb2.TILE_DOOR_BLUE)
        else:
            plate_doors = []

        if np.any(plate_doors):
            # player 1 stands on plates
            # player 2 moves to plate doors
            if self.can_act2() and valid_pos(self.player1_pos): # Only with two players
                self.move_to_closest_1(plate_doors, 'plate_door')


    def pickup_boulder(self) -> None:
        boulders = np.argwhere(self.map == swoq_pb2.TILE_BOULDER)
        boulders = [b for b in boulders if tuple(b) not in self.plates_with_boulders]
        if np.any(boulders):
            if self.can_act1() and self.player1_inventory == 0:
                self.use_closest_1(boulders, 'boulder')
            if self.can_act2() and self.player2_inventory == 0:
                self.use_closest_2(boulders, 'boulder')


    def wait_at_random_door(self) -> None:
        doors = np.argwhere((self.map == swoq_pb2.TILE_DOOR_RED) | (self.map == swoq_pb2.TILE_DOOR_GREEN) | (self.map == swoq_pb2.TILE_DOOR_BLUE))
        for door_pos in doors:
            door_pos = tuple(door_pos)

            # Do not move to door if other player has key for it
            door_tile = self.map[door_pos]

            player1_has_key = (door_tile == swoq_pb2.TILE_DOOR_RED and self.player1_inventory == swoq_pb2.INVENTORY_KEY_RED) or \
                (door_tile == swoq_pb2.TILE_DOOR_GREEN and self.player1_inventory == swoq_pb2.INVENTORY_KEY_GREEN) or \
                (door_tile == swoq_pb2.TILE_DOOR_BLUE and self.player1_inventory == swoq_pb2.INVENTORY_KEY_BLUE)

            player2_has_key = (door_tile == swoq_pb2.TILE_DOOR_RED and self.player2_inventory == swoq_pb2.INVENTORY_KEY_RED) or \
                (door_tile == swoq_pb2.TILE_DOOR_GREEN and self.player2_inventory == swoq_pb2.INVENTORY_KEY_GREEN) or \
                (door_tile == swoq_pb2.TILE_DOOR_BLUE and self.player2_inventory == swoq_pb2.INVENTORY_KEY_BLUE)

            if self.can_act1() and not player2_has_key and self.can_1_reach(door_pos):
                if euclid_dist(self.player1_pos, door_pos) > 1:
                    print('move_random_door1')
                    self.move_to_1(door_pos)
            elif self.can_act2() and not player1_has_key and self.can_2_reach(door_pos):
                if euclid_dist(self.player2_pos, door_pos) > 1:
                    print('move_random_door2')
                    self.move_to_2(door_pos)


    def handle_level18(self) -> None:
        if valid_pos(self.player1_pos) and self.player1_pos[1] < 8:
            # Move player 2 to plate
            plates = np.argwhere((self.map == swoq_pb2.TILE_PRESSURE_PLATE_RED) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_GREEN) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_BLUE))
            plates = list([p for p in plates if p[1] < 8])
            if np.any(plates) and self.can_act2():
                self.plate_pos_2 = tuple(plates[0])
                self.plate_color_2 = self.map[self.plate_pos_2]
                print('plate2_18')
                self.move_to_2(self.plate_pos_2)
        else:
            self.remain_on_plate_counter_2 = 0

        if valid_pos(self.player2_pos) and self.player2_pos[0] < 11:
            # Move player 1 to second plate
            plates = np.argwhere((self.map == swoq_pb2.TILE_PRESSURE_PLATE_RED) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_GREEN) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_BLUE))
            plates = list([p for p in plates if p[1] >= 8])
            if np.any(plates) and self.can_act1():
                self.plate_pos_1 = tuple(plates[0])
                self.plate_color_1 = self.map[self.plate_pos_1]
                print('plate1_18')
                self.move_to_1(self.plate_pos_1)
        else:
            self.remain_on_plate_counter_1 = 0


    def can_1_reach(self, pos) -> bool:
        return get_direction(self.player1_pos, pos, self.player1_distances, self.player1_paths) is not None

    def can_2_reach(self, pos) -> bool:
        return get_direction(self.player2_pos, pos, self.player2_distances, self.player2_paths) is not None


    def use_closest_1(self, positions, name) -> tuple[int, int]|None:
        if not self.can_act1():
            return
        pos, dir = self.find_closest(positions, self.player1_pos, self.player1_distances, self.player1_paths)
        if dir is None or pos is None:
            return None
    
        if are_adjacent(self.player1_pos, pos):
            print(f'{name}_use_1')
            self.queue_use1(dir)
        else:
            print(f'{name}_move_1')
            self.queue_move1(dir)
        return pos

    def use_closest_2(self, positions, name) -> tuple[int, int]|None:
        if not self.can_act2():
            return
        pos, dir = self.find_closest(positions, self.player2_pos, self.player2_distances, self.player2_paths)
        if dir is None or pos is None:
            return None
    
        if are_adjacent(self.player2_pos, pos):
            print(f'{name}_use_2')
            self.queue_use2(dir)
        else:
            print(f'{name}_move_2')
            self.queue_move2(dir)
        return pos

    def move_to_closest_1(self, positions, name):
        if not self.can_act1():
            return None

        pos, dir = self.find_closest(positions, self.player1_pos, self.player1_distances, self.player1_paths)
        if dir is None or pos is None:
            return None
    
        print(f'{name}_move_1')
        self.queue_move1(dir)
        return pos
    
    def move_to_closest_2(self, positions, name):
        if not self.can_act2():
            return None

        pos, dir = self.find_closest(positions, self.player2_pos, self.player2_distances, self.player2_paths)
        if dir is None or pos is None:
            return None
    
        print(f'{name}_move_2')
        self.queue_move2(dir)
        return pos

    def find_closest(self, positions, player_pos, player_distances, player_paths) -> tuple[tuple[int,int]|None, str|None]:
        min_dist = None
        min_dir = None
        min_pos = None
        for pos in positions:
            pos = tuple(pos)
            dir, dist = get_direction_and_distance(player_pos, pos, player_distances, player_paths)
            if min_dist is None or dist < min_dist:
                min_dist = dist
                min_dir = dir
                min_pos = pos
                
        return min_pos, min_dir



    def update_remain_on_plate(self) -> None:
        if self.plate_pos_1 is not None and self.player1_pos[0] == self.plate_pos_1[0] and self.player1_pos[1] == self.plate_pos_1[1]:
            print(f'Reset 1 {self.remain_on_plate_counter_1=}')
            self.remain_on_plate_counter_1 = 100
            
        if self.remain_on_plate_counter_1 > 0:
            self.remain_on_plate_counter_1 -= 1

        if self.plate_pos_2 is not None and self.player2_pos[0] == self.plate_pos_2[0] and self.player2_pos[1] == self.plate_pos_2[1]:
            print(f'Reset 2 {self.remain_on_plate_counter_2=}')
            self.remain_on_plate_counter_2 = 100
            
        if self.remain_on_plate_counter_2 > 0:
            self.remain_on_plate_counter_2 -= 1
