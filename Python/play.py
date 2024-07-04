import grpc
import swoq_pb2
import swoq_pb2_grpc
import numpy as np
from map_util import *
from time import sleep

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
    swoq_pb2.QUEST_QUEUED: 'QUEST_QUEUED',

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
    swoq_pb2.NO_PROGRESS: 'NO_PROGRESS',
    swoq_pb2.GAME_TIMEOUT: 'GAME_TIMEOUT',
}

class GamePlayer:

    def __init__(self, player_id:str, plot:bool=True, print:bool=True):
        self.player_id = player_id
        self.plot = plot
        self.print = print

        self.remain_on_plate_counter = 0

        self.channel = grpc.insecure_channel('localhost:5009')
        self.stub = swoq_pb2_grpc.GameServiceStub(self.channel)


    def close(self) -> None:
        self.channel.close()


    def __enter__(self) -> object:
        return self


    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.close()


    def start(self, level:int=None) -> None:
        startResponse = self.stub.Start(swoq_pb2.StartRequest(playerId=self.player_id, level=level))
        if self.print:
            result = _result_strings[startResponse.result]
            print(f'{result=}')
        
        while startResponse.result == swoq_pb2.QUEST_QUEUED:
            sleep(1)
            startResponse = self.stub.Start(swoq_pb2.StartRequest(playerId=self.player_id, level=level))
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
        # Once in a while player 2 will not move, to break cycles
        if np.random.uniform() < 0.05:
            print(f' player 2 skipped')
            self.action2 = None
            self.direction2 = None
            
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

        self.move_to_exit()
        self.attack()
        self.pickup_health()
        self.pickup_sword()
        self.pickup_keys()
        self.explore()
        self.move_to_pressure_plate()
        self.wait_at_black_door()
        self.pickup_boulder()
        self.act()
        self.update_remain_on_plate()


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


    def can_act1(self) -> bool:
        return valid_pos(self.player1_pos) and self.action1 is None and self.remain_on_plate_counter <= 0


    def can_act2(self) -> bool:
        return valid_pos(self.player2_pos) and self.action2 is None


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
            door_pos = tuple(doors[0])
            if self.can_act1() and self.player1_inventory == item:
                if are_adjacent(self.player1_pos, door_pos):
                    print('use_door1')
                    self.use_1(door_pos)
                else:
                    print('move_door1')
                    self.move_to_1(door_pos)
            elif self.can_act2() and self.player2_inventory == item:
                if are_adjacent(self.player2_pos, door_pos):
                    print('use_door2')
                    self.use_2(door_pos)
                else:
                    print('move_door2')
                    self.move_to_2(door_pos)
            else:
                keys = np.argwhere(self.map == key)
                if np.any(keys):

                    if self.can_act1() and self.player1_inventory == 0:
                        for key in keys:
                            key_pos = tuple(key)
                            if get_direction(self.player1_pos, key_pos, self.player1_distances, self.player1_paths) is not None:
                                print('move_key1')
                                self.move_to_1(key_pos)
                                break

                    if self.can_act2() and self.player2_inventory == 0:
                        for key in keys:
                            key_pos = tuple(key)
                            if get_direction(self.player2_pos, key_pos, self.player2_distances, self.player2_paths) is not None:
                                print('move_key2')
                                self.move_to_2(key_pos)
                                break


    def move_to_exit(self) -> None:
        # Move to exit if possible
        exits = np.argwhere(self.map == EXIT)
        if np.any(exits):
            exit_pos = tuple(exits[0])
            
            # Only move when both players can reach the exit (or each other)
            can_reach = False
            if valid_pos(self.player1_pos) and valid_pos(self.player2_pos):
                can_reach = (get_direction(self.player1_pos, exit_pos, self.player1_distances, self.player1_paths) is not None and \
                    get_direction(self.player1_pos, self.player1_pos, self.player1_distances, self.player1_paths) is not None) or \
                    (get_direction(self.player1_pos, self.player2_pos, self.player1_distances, self.player1_paths) is not None and \
                    get_direction(self.player1_pos, exit_pos, self.player1_distances, self.player1_paths) is not None)
            elif valid_pos(self.player1_pos):
                can_reach = get_direction(self.player1_pos, exit_pos, self.player1_distances, self.player1_paths) is not None
            elif valid_pos(self.player2_pos):
                can_reach = get_direction(self.player2_pos, exit_pos, self.player2_distances, self.player2_paths) is not None
                
            if can_reach:
                if self.can_act1():
                    print('exit1')
                    self.move_to_1(exit_pos)
                if self.can_act2():
                    print('exit2')
                    self.move_to_2(exit_pos)


    def attack(self) -> None:
        # Attack
        enemies = np.argwhere(self.map == ENEMY)
        if np.any(enemies):
            enemy_pos = tuple(enemies[0])

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
            print(f'dist: 1>2: {dist_1_to_2}, 2>1: {dist_2_to_1}')

            if self.can_act1() and self.player1_has_sword:
                can_attack = True
                if dist_players is not None and dist_players > 5:
                    print('move_closer1')
                    self.move_to_1(self.player2_pos)
                    can_attack = False

                if can_attack:
                    if are_adjacent(self.player1_pos, enemy_pos):
                        print('attack_use1')
                        self.use_1(enemy_pos)
                    else:
                        print('attack_move1')
                        self.move_to_1(enemy_pos)

            if self.can_act2() and self.player2_has_sword:
                can_attack = True
                if dist_players is not None and dist_players > 4:
                    print('move_closer2')
                    self.move_to_2(self.player1_pos)
                    can_attack = False

                if can_attack:
                    if are_adjacent(self.player2_pos, enemy_pos):
                        print('attack_use2')
                        self.use_2(enemy_pos)
                    else:
                        print('attack_move2')
                        self.move_to_2(enemy_pos)


    def pickup_health(self) -> None:
        # Pickup health
        healths = np.argwhere(self.map == HEALTH)
        if np.any(healths):
            health_pos = tuple(healths[0])
            if self.can_act1() and self.player1_health <= 5:
                print('health1')
                self.move_to_1(health_pos)
            elif self.can_act2() and self.player2_health <= 5:
                print('health2')
                self.move_to_2(health_pos)


    def pickup_sword(self) -> None:
        # Pickup sword
        swords = np.argwhere(self.map == SWORD)
        if np.any(swords):
            sword_pos = tuple(swords[0])
            if self.can_act1() and not self.player1_has_sword:
                print('sword1')
                self.move_to_1(sword_pos)
            elif self.can_act2() and not self.player2_has_sword:
                print('sword2')
                self.move_to_2(sword_pos)


    def pickup_keys(self) -> None:
        # Pickup keys
        self.pickup_key_or_open_door(KEY_RED, DOOR_RED, INVENTORY_KEY_RED)
        self.pickup_key_or_open_door(KEY_GREEN, DOOR_GREEN, INVENTORY_KEY_GREEN)
        self.pickup_key_or_open_door(KEY_BLUE, DOOR_BLUE, INVENTORY_KEY_BLUE)


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


    def move_to_pressure_plate(self) -> None:
        plates = np.argwhere(self.map == PRESSURE_PLATE)
        self.plate_pos = None
        if np.any(plates):
            self.plate_pos = tuple(plates[0])
            if self.can_act1():
                if valid_pos(self.player2_pos): # Only with two players simply move
                    print('plate1')
                    self.move_to_1(self.plate_pos)
                elif self.player1_inventory == 4: # has boulder
                    print('plate_boulder1')
                    if are_adjacent(self.player1_pos, self.plate_pos):
                        self.use_1(self.plate_pos)
                    else:
                        # move to below plate if possible
                        below = (self.plate_pos[0]+1, self.plate_pos[1])
                        self.move_to_1(below if not is_wall(self.map, below) else self.plate_pos)


    def wait_at_black_door(self) -> None:
        black_doors = np.argwhere(self.map == DOOR_BLACK)
        if np.any(black_doors):
            black_door_pos = tuple(black_doors[0])
            if self.can_act2() and valid_pos(self.player1_pos): # Only with two players
                print('move_black2')
                self.move_to_2(black_door_pos)


    def pickup_boulder(self) -> None:
        boulders = np.argwhere(self.map == BOULDERS)
        if np.any(boulders):
            boulder_pos = tuple(boulders[0])
            if self.can_act1() and self.player1_inventory == 0:
                if are_adjacent(self.player1_pos, boulder_pos):
                    print('use_boulder1')
                    self.use_1(boulder_pos)
                else:
                    print('move_boulder1')
                    self.move_to_1(boulder_pos)                
            if self.can_act2() and self.player2_inventory == 0:
                if are_adjacent(self.player2_pos, boulder_pos):
                    print('use_boulder2')
                    self.use_2(boulder_pos)
                else:
                    print('move_boulder2')
                    self.move_to_2(boulder_pos)                

                
    def update_remain_on_plate(self) -> None:
        if self.plate_pos is not None and self.player1_pos[0] == self.plate_pos[0] and self.player1_pos[1] == self.plate_pos[1]:
            print(f'Reset {self.remain_on_plate_counter=}')
            self.remain_on_plate_counter = 100
        self.remain_on_plate_counter -= 1
