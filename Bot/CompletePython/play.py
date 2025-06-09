import grpc
import swoq_pb2
import swoq_pb2_grpc
import numpy as np
from map_util import *
from time import sleep

to_swoq_pb2_action = {
    'MN': swoq_pb2.DIRECTED_ACTION_MOVE_NORTH,
    'ME': swoq_pb2.DIRECTED_ACTION_MOVE_EAST,
    'MS': swoq_pb2.DIRECTED_ACTION_MOVE_SOUTH,
    'MW': swoq_pb2.DIRECTED_ACTION_MOVE_WEST,
    'UN': swoq_pb2.DIRECTED_ACTION_USE_NORTH,
    'UE': swoq_pb2.DIRECTED_ACTION_USE_EAST,
    'US': swoq_pb2.DIRECTED_ACTION_USE_SOUTH,
    'UW': swoq_pb2.DIRECTED_ACTION_USE_WEST,
    None: None,
}

def find_random_pos(player_pos, player_distances) -> tuple[int,int]|None:
    positions = list(player_distances.keys())
    if player_pos in positions:
        positions.remove(player_pos) # not own pos
    if np.any(positions):
        return positions[np.random.choice(len(positions))]
    else:
        return None


class GamePlayer:

    def __init__(self, user_id:str, plot:bool=True, print:bool=True):
        self.user_id = user_id
        self.plot = plot
        self.print = print
        
        self.actions = []

        self.remain_on_plate_counter = 0
        self.plate_color = None

        self.channel = grpc.insecure_channel('localhost:5001')
        self.stub = swoq_pb2_grpc.GameServiceStub(self.channel)


    def close(self) -> None:
        self.channel.close()


    def __enter__(self) -> object:
        return self


    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.close()


    def start(self, level:int=None, seed:int=None) -> None:
        startResponse = self.stub.Start(swoq_pb2.StartRequest(userId=self.user_id, level=level, seed=seed))
        if self.print:
            result = swoq_pb2.StartResult.Name(startResponse.result)
            print(f'{result=}')

        while startResponse.result == swoq_pb2.START_RESULT_QUEST_QUEUED:
            sleep(1)
            startResponse = self.stub.Start(swoq_pb2.StartRequest(userId=self.user_id, level=level, seed=seed))
            if self.print:
                result = swoq_pb2.StartResult.Name(startResponse.result)
                print(f'{result=}')
        
        if startResponse.result != swoq_pb2.START_RESULT_OK:
            raise Exception(f'Failed to start game: {startResponse.result}')

        self.game_id = startResponse.gameId
        self.height = startResponse.mapHeight
        self.width = startResponse.mapWidth
        self.visibility_range = startResponse.visibilityRange
        self.actions = []

        self.prev_level = -1
        self.map = np.zeros((self.height, self.width), dtype=np.int8)

        self.update_global_state(startResponse.state)

        self.action1:swoq_pb2.DirectedAction = None
        self.action2:swoq_pb2.DirectedAction = None

        self.reset()

        print(f'Started game {self.game_id}, level {self.level}')

        if self.plot:
            self._frame = plot_map(self.map)


    def reset(self) -> None:
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
        self.picked_up_boulders = set()
        self.plate_door_positions = set()
        self.level22_prev_boss_pos = None
        self.level22_state = None


    def update_global_state(self, state:swoq_pb2.State) -> None:
        self.level = state.level
        self.status = state.status
        self.finished = state.status != swoq_pb2.GAME_STATUS_ACTIVE

        self.combined_health = 0

        self.player1_pos = (state.playerState.position.y, state.playerState.position.x) if state.HasField('playerState') else None
        self.player1_health = state.playerState.health if state.HasField('playerState') else None
        self.player1_inventory = state.playerState.inventory if state.HasField('playerState') else None
        self.player1_has_sword = state.playerState.hasSword if state.HasField('playerState') else None
        if self.player1_health is not None:
            self.combined_health += self.player1_health

        self.player2_pos = (state.player2State.position.y, state.player2State.position.x) if state.HasField('player2State') else None
        self.player2_health = state.player2State.health if state.HasField('player2State') else None
        self.player2_inventory = state.player2State.inventory if state.HasField('player2State') else None
        self.player2_has_sword = state.player2State.hasSword if state.HasField('player2State') else None
        if self.player2_health is not None:
            self.combined_health += self.player2_health

        self.two_players = valid_pos(self.player1_pos) and valid_pos(self.player2_pos)

        if self.print:
            print(f'status={self.status} level={self.level}, comb health={self.combined_health}')
            print(f'player1: pos={self.player1_pos}, health={self.player1_health}, inventory={self.player1_inventory}, has_sword={self.player1_has_sword}')
            print(f'player2: pos={self.player2_pos}, health={self.player2_health}, inventory={self.player2_inventory}, has_sword={self.player2_has_sword}')

        # Clear map for every new level
        if self.prev_level != self.level:
            self.prev_level = self.level
            if self.print: print(f'Entered level {self.level}')
            self.reset()

        # Copy surroundings to map
        if len(state.playerState.surroundings) > 0:
            top = self.player1_pos[0] - self.visibility_range
            left = self.player1_pos[1] - self.visibility_range
            i = 0
            for y in range(self.visibility_range*2 + 1):
                for x in range(self.visibility_range*2 + 1):
                    s = state.playerState.surroundings[i]
                    if s != swoq_pb2.TILE_UNKNOWN:
                        map_x = left + x
                        map_y = top + y
                        if 0 <= map_y < self.map.shape[0] and 0 <= map_x < self.map.shape[1]:
                            self.map[map_y, map_x] = s
                    i += 1

        if len(state.player2State.surroundings) > 0:
            top = self.player2_pos[0] - self.visibility_range
            left = self.player2_pos[1] - self.visibility_range
            i = 0
            for y in range(self.visibility_range*2 + 1):
                for x in range(self.visibility_range*2 + 1):
                    s = state.player2State.surroundings[i]
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
        if not self.print: print('.', end='', flush=True)
        
        # Once in a while player 2 will not move, to break cycles
        if np.random.uniform() < 0.05:
            if self.print: print(f' player 2 skipped')
            self.action2 = None

        # -1 means stay at position
        if self.action1 == -1:
            self.action1 = None
        if self.action2 == -1:
            self.action2 = None

        if self.print: print(f'{self.action1=}, {self.action2=}')
        response = self.stub.Act(swoq_pb2.ActRequest(gameId=self.game_id, action=self.action1, action2=self.action2))

        if response.result == swoq_pb2.ACT_RESULT_OK:
            self.actions.append((self.action1, self.action2))

        self.update_global_state(response.state)

        if self.plot:
            update_map(self._frame, self.map)

        if self.print:
            result = swoq_pb2.ActResult.Name(response.result)
            print(f'{result=}')
            print(f' finished={self.finished}')
            
        if not self.print and self.finished:
            print()
            print(f'Finished: action {swoq_pb2.ActResult.Name(response.result)}, status {swoq_pb2.GameStatus.Name(self.status)} ')

        # clear for next act
        self.action1:swoq_pb2.DirectedAction = None
        self.action2:swoq_pb2.DirectedAction = None

        return response.result == 0


    def step_randomly(self) -> None:
        assert(self.action1 is None)
        self.action1 = np.random.choice(swoq_pb2.DirectedAction.values())
        if self.two_players:
            assert(self.action2 is None)
            self.action2 = np.random.choice(swoq_pb2.DirectedAction.values())
        self.act()
        self.update_remain_on_plate()
        

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

        self.store_plate_door_positions()

        if self.level == 20:
            self.step_level20()
        elif self.level == 21:
            self.step_level21()
        elif self.level == 22:
            self.step_level22()
        else:
            self.move_to_exit()
            self.pickup_health()
            self.pickup_sword()
            self.pickup_keys_or_open_doors()
            self.attack()
            self.crush_with_door()
            self.explore()
            self.move_to_pressure_plate()
            self.wait_at_pressure_plate_door_2()
            self.pickup_boulder()
            self.wait_at_random_door()
        self.random_walk() # fallback
        self.act()
        self.update_remain_on_plate()

    def step_level20(self) -> None:
        self.handle_level20()
        self.move_to_exit()
        self.pickup_health()
        self.pickup_sword()
        self.pickup_keys_or_open_doors()
        self.attack()
        self.explore()
        self.pickup_boulder()


    def step_level21(self) -> None:
        old_map = self.map

        # as long as both players have no sword the bottom right part is off-limits
        if not self.player1_has_sword or not self.player2_has_sword:
            new_map = self.map.copy()
            for my in range(self.height-19, self.height):
                for mx in range(self.width-12, self.width):
                    new_map[my, mx] = swoq_pb2.TILE_WALL
                    if (my, mx) in self.player1_distances:
                        del self.player1_distances[(my, mx)]
                    if (my, mx) in self.player2_distances:
                        del self.player2_distances[(my, mx)]
            self.map = new_map

        self.move_to_exit()
        self.pickup_boulder()
        self.level21_place_boulder()
        if not self.player1_has_sword and len(self.plates_with_boulders) > 0:
            self.level21_wait_at_plate_2()
        if self.player1_has_sword and not self.player2_has_sword and len(self.plates_with_boulders) > 0:
            self.level21_wait_at_plate_1()
        self.pickup_health()
        self.pickup_sword()
        self.pickup_keys_or_open_doors()
        self.attack()
        self.explore()
        # self.move_to_pressure_plate()
        self.wait_at_pressure_plate_door_1()
        self.wait_at_pressure_plate_door_2()

        self.map = old_map


    def step_level22(self) -> None:
        old_map = self.map

        player_1_on_plate = self.plate_color_1 is not None and self.remain_on_plate_counter_1 > 0

        if self.level22_state is None:
            self.level22_state = 'explore'

        avoid_boss = False
        boss_positions = np.argwhere(self.map == swoq_pb2.TILE_BOSS)
        if np.any(boss_positions):
            boss_pos = tuple(boss_positions[0])
            boss_pos_y, boss_pos_x = boss_pos
            # Stop avoiding boss when player 1 is on the plate
            avoid_boss = not player_1_on_plate
        else:
            boss_pos = None

        boss_moving = False
        if boss_pos is not None:
            if self.level22_prev_boss_pos is not None and self.level22_prev_boss_pos != boss_pos:
                boss_moving = True
            self.level22_prev_boss_pos = boss_pos

        # stay away from boss if not done yet
        if avoid_boss:
            new_map = self.map.copy()
            for my in range(boss_pos_y-self.visibility_range, boss_pos_y+self.visibility_range+1):
                for mx in range(boss_pos_x-self.visibility_range, boss_pos_x+self.visibility_range+1):
                    if 0 <= my < self.height and 0 <= mx < self.width:
                        new_map[my, mx] = swoq_pb2.TILE_WALL
                        if (my, mx) in self.player1_distances:
                            del self.player1_distances[(my, mx)]
                        if (my, mx) in self.player2_distances:
                            del self.player2_distances[(my, mx)]
            self.map = new_map

        if self.level22_state == 'explore':
            if np.any(self.plate_door_positions):
                self.level22_state = 'move_to_plate'

        if self.level22_state == 'move_to_plate':
            # player 1 on plate
            self.level21_wait_at_plate_1()
            # move player 2 towards player 1
            if self.can_act2():
                dir = get_direction_towards(self.player2_paths, self.player2_pos, self.player1_pos)
                self.queue_move2(dir)

            if self.remain_on_plate_counter_1 > 0:
                self.level22_state = 'move_to_door'

        if self.level22_state == 'move_to_door':
            self.remain_on_plate_counter_1 = 100 # keep player 1 on plate
            if self.player2_pos in self.plate_door_positions:
                self.level22_state = 'trigger_boss'
            else:
                if self.can_act2():
                    is_adjacent = any(are_adjacent(self.player2_pos, pos) for pos in self.plate_door_positions)
                    if not is_adjacent:
                        self.move_to_closest_2(self.plate_door_positions, 'boss_door')

        if self.level22_state == 'trigger_boss':
            self.remain_on_plate_counter_1 = 100 # keep player 1 on plate
            if boss_moving:
                self.level22_state = 'lure_boss'
            else:
                if self.can_act2():
                    self.queue_move2('E')

        if self.level22_state == 'lure_boss':
            self.remain_on_plate_counter_1 = 100
            if self.can_act2():
                if boss_moving:
                    self.queue_move2('W')
                else:
                    # stay at position, don't allow other actions
                    self.action2 = -1

            # move player 1 off plate when boss is at door
            if boss_pos in self.plate_door_positions:
                self.remain_on_plate_counter_1 = 0
                self.plate_pos_1 = None
                self.plate_color_1 = None
                self.queue_move1('W') #override any move
                self.level22_state = 'open_exit'

        if self.level22_state == 'open_exit':
            self.pickup_keys_or_open_doors()

            if np.any(self.map == swoq_pb2.TILE_EXIT):
                self.level22_state = 'pickup_treasure'

        if self.level22_state == 'pickup_treasure':
            self.pickup_treasure()
            if self.player1_inventory == swoq_pb2.INVENTORY_TREASURE and self.player2_inventory == swoq_pb2.INVENTORY_TREASURE:
                self.level22_state = 'move_to_exit'

        if self.level22_state == 'move_to_exit':
            self.move_to_exit()

        self.explore()

        self.map = old_map


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
                                if self.print: print('boulder_drop1')
                                self.use_1(self.boulder_drop_pos_1)
                                self.boulder_drop_pos_1 = None
                            else:
                                if self.print: print('boulder_drop_move1')
                                self.move_to_1(self.boulder_drop_pos_1)
                    else:
                        if self.print: print('exit1')
                        self.move_to_1(exit_pos)
                if self.can_act2():
                    # drop boulder before entering exit
                    if self.player2_inventory == swoq_pb2.INVENTORY_BOULDER:
                        if self.boulder_drop_pos_2 is None:
                            self.boulder_drop_pos_2 = self.get_closest_clearing(self.player2_pos, self.player2_distances, self.player2_paths)
                        if self.boulder_drop_pos_2 is not None:
                            if are_adjacent(self.player2_pos, self.boulder_drop_pos_2):
                                if self.print: print('boulder_drop2')
                                self.use_2(self.boulder_drop_pos_2)
                                self.boulder_drop_pos_2 = None
                            else:
                                if self.print: print('boulder_drop_move2')
                                self.move_to_2(self.boulder_drop_pos_2)
                    else:
                        if self.print: print('exit2')
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
        can_attack_1 = self.player1_has_sword and self.player1_health > 1
        can_attack_2 = self.player2_has_sword and self.player2_health > 1

        # Attack
        enemies = np.argwhere(self.map == swoq_pb2.TILE_ENEMY)
        if np.any(enemies):
            if self.expected_enemy_health is None:
                self.expected_enemy_health = 6

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
                    if self.print: print('move_closer_1')
                    self.move_to_1(self.player2_pos)
                if self.can_act2() and dist_players is not None and dist_players > 4:
                    if self.print: print('move_closer_2')
                    self.move_to_2(self.player1_pos)

            if self.can_act1() and can_attack_1:
                _, attacked = self.use_closest_1(enemies, 'attack')
                if attacked:
                    self.expected_enemy_health -= 1

            if self.can_act2() and can_attack_2:
                _, attacked = self.use_closest_2(enemies, 'attack')
                if attacked:
                    self.expected_enemy_health -= 1

            if self.expected_enemy_health is not None and self.expected_enemy_health <= 0:
                self.expected_enemy_health = None

        elif self.expected_enemy_health is not None and self.expected_enemy_health > 0:
            # if there was an enemy but it is no longer visible, then find it by walking randomly
            if self.can_act1() and can_attack_1:
                self.explore()
                self.random_walk()

            if self.can_act2() and can_attack_2:
                self.explore()
                self.random_walk()


    def crush_with_door(self) -> None:
        player_1_on_plate = self.plate_color_1 is not None and self.remain_on_plate_counter_1 > 0
        player_2_on_plate = self.plate_color_2 is not None and self.remain_on_plate_counter_2 > 0

        enemy_at_plate_door = False

        enemies = np.argwhere((self.map == swoq_pb2.TILE_ENEMY) | (self.map == swoq_pb2.TILE_BOSS))
        for enemy_pos in enemies:
            enemy_pos = tuple(enemy_pos)
            if enemy_pos in self.plate_door_positions:
                enemy_at_plate_door = True

        if enemy_at_plate_door:
            if player_1_on_plate:
                self.remain_on_plate_counter_1 = 0
                self.plate_pos_1 = None
                self.plate_color_1 = None
                self.queue_move1('W') #override any move
            if player_2_on_plate:
                self.remain_on_plate_counter_2 = 0
                self.plate_pos_2 = None
                self.plate_color_2 = None
                self.queue_move2('W') #override any move


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


    def pickup_keys_or_open_doors(self) -> None:
        # Pickup keys
        self.pickup_key_or_open_door(swoq_pb2.TILE_KEY_RED, swoq_pb2.TILE_DOOR_RED, swoq_pb2.INVENTORY_KEY_RED)
        self.pickup_key_or_open_door(swoq_pb2.TILE_KEY_GREEN, swoq_pb2.TILE_DOOR_GREEN, swoq_pb2.INVENTORY_KEY_GREEN)
        self.pickup_key_or_open_door(swoq_pb2.TILE_KEY_BLUE, swoq_pb2.TILE_DOOR_BLUE, swoq_pb2.INVENTORY_KEY_BLUE)


    def pickup_treasure(self) -> None:
        treasures = np.argwhere(self.map == swoq_pb2.TILE_TREASURE)
        if np.any(treasures):
            if self.can_act1() and self.player1_inventory == 0:
                self.move_to_closest_1(treasures, 'treasure')
            if self.can_act2() and self.player2_inventory == 0:
                self.move_to_closest_2(treasures, 'treasure')


    def explore(self) -> None:
        # Explore
        if self.can_act1():
            dir = self.get_direction_towards_closest_unknown(self.player1_pos, self.player1_distances, self.player1_paths)
            if dir is not None:
                if self.print: print('explore1')
                self.queue_move1(dir)
        if self.can_act2():
            dir = self.get_direction_towards_closest_unknown(self.player2_pos, self.player2_distances, self.player2_paths)
            if dir is not None:
                if self.print: print('explore2')
                self.queue_move2(dir)


    def random_walk(self) -> None:
        if self.random_pos1 is not None:
            if self.player1_pos == self.random_pos1 or not self.can_1_reach(self.random_pos1):
                self.random_pos1 = None

        if self.random_pos2 is not None:
            if self.player2_pos == self.random_pos2 or not self.can_2_reach(self.random_pos2):
                self.random_pos2 = None

        if self.can_act1():
            if self.random_pos1 is None:
                self.random_pos1 = find_random_pos(self.player1_pos, self.player1_distances)
            if self.random_pos1 is not None:
                if self.print: print(f'random1 {self.random_pos1}')
                self.move_to_1(self.random_pos1)
        if self.can_act2():
            if self.random_pos2 is None:
                self.random_pos2 = find_random_pos(self.player2_pos, self.player2_distances)
            if self.random_pos2 is not None:
                if self.print: print(f'random2 {self.random_pos2}')
                self.move_to_2(self.random_pos2)


    def move_to_pressure_plate(self) -> None:
        plates = np.argwhere((self.map == swoq_pb2.TILE_PRESSURE_PLATE_RED) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_GREEN) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_BLUE))
        if np.any(plates):
            if self.can_act1():
                if self.player1_inventory == 4:
                    # place boulders on plates
                    plate_pos, placed = self.use_closest_1(plates, 'plate_boulder')
                    if plate_pos is not None:
                        self.plate_pos_1 = plate_pos
                        self.plate_color_1 = self.map[self.plate_pos_1]
                    if placed:
                        self.plates_with_boulders.append(self.plate_pos_1)
                elif self.two_players or self.level == 9:
                    # only player 1 will stand on plates with two players
                    plate_pos = self.move_to_closest_1(plates, 'plate')
                    if plate_pos is not None:
                        self.plate_pos_1 = plate_pos
                        self.plate_color_1 = self.map[self.plate_pos_1]

            if self.can_act2() and self.player2_inventory == 4:
                # place boulders on plates
                plate_pos, placed = self.use_closest_2(plates, 'plate_boulder')
                if plate_pos is not None:
                    self.plate_pos_2 = plate_pos
                    self.plate_color_2 = self.map[self.plate_pos_2]
                if placed:
                    self.plates_with_boulders.append(self.plate_pos_2)


    def wait_at_pressure_plate_door_1(self) -> None:
        if self.plate_color_2 == swoq_pb2.TILE_PRESSURE_PLATE_RED:
            plate_doors = np.argwhere(self.map == swoq_pb2.TILE_DOOR_RED)
        elif self.plate_color_2  == swoq_pb2.TILE_PRESSURE_PLATE_GREEN:
            plate_doors = np.argwhere(self.map == swoq_pb2.TILE_DOOR_GREEN)
        elif self.plate_color_2  == swoq_pb2.TILE_PRESSURE_PLATE_BLUE:
            plate_doors = np.argwhere(self.map == swoq_pb2.TILE_DOOR_BLUE)
        else:
            plate_doors = []

        if np.any(plate_doors):
            # player 2 stands on plates
            # player 1 moves to plate doors
            if self.can_act1() and valid_pos(self.player2_pos): # Only with two players
                in_front_positions = []
                for d in plate_doors:
                    d = tuple(d)
                    if self.map[d[0]-1, d[1]] == swoq_pb2.TILE_EMPTY: in_front_positions.append((d[0]-1, d[1]))
                    if self.map[d[0]+1, d[1]] == swoq_pb2.TILE_EMPTY: in_front_positions.append((d[0]+1, d[1]))
                    if self.map[d[0], d[1]-1] == swoq_pb2.TILE_EMPTY: in_front_positions.append((d[0], d[1]-1))
                    if self.map[d[0], d[1]+1] == swoq_pb2.TILE_EMPTY: in_front_positions.append((d[0], d[1]+1))
                
                self.move_to_closest_1(in_front_positions, 'plate_door')

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
                in_front_positions = []
                for d in plate_doors:
                    d = tuple(d)
                    if self.map[d[0]-1, d[1]] == swoq_pb2.TILE_EMPTY: in_front_positions.append((d[0]-1, d[1]))
                    if self.map[d[0]+1, d[1]] == swoq_pb2.TILE_EMPTY: in_front_positions.append((d[0]+1, d[1]))
                    if self.map[d[0], d[1]-1] == swoq_pb2.TILE_EMPTY: in_front_positions.append((d[0], d[1]-1))
                    if self.map[d[0], d[1]+1] == swoq_pb2.TILE_EMPTY: in_front_positions.append((d[0], d[1]+1))

                self.move_to_closest_2(in_front_positions, 'plate_door')


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
                    if self.print: print('move_random_door1')
                    self.move_to_1(door_pos)
            elif self.can_act2() and not player1_has_key and self.can_2_reach(door_pos):
                if euclid_dist(self.player2_pos, door_pos) > 1:
                    if self.print: print('move_random_door2')
                    self.move_to_2(door_pos)


    def handle_level20(self) -> None:
        if valid_pos(self.player1_pos) and self.player1_pos[1] < 8:
            # Move player 2 to plate
            plates = np.argwhere((self.map == swoq_pb2.TILE_PRESSURE_PLATE_RED) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_GREEN) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_BLUE))
            plates = list([p for p in plates if p[1] < 8])
            if np.any(plates) and self.can_act2():
                self.plate_pos_2 = tuple(plates[0])
                self.plate_color_2 = self.map[self.plate_pos_2]
                if self.print: print('plate2_18')
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
                if self.print: print('plate1_18')
                self.move_to_1(self.plate_pos_1)
        else:
            self.remain_on_plate_counter_1 = 0


    def can_1_reach(self, pos) -> bool:
        return get_direction(self.player1_pos, pos, self.player1_distances, self.player1_paths) is not None

    def can_2_reach(self, pos) -> bool:
        return get_direction(self.player2_pos, pos, self.player2_distances, self.player2_paths) is not None


    def use_closest_1(self, positions, name) -> tuple[tuple[int, int]|None, bool]:
        if not self.can_act1():
            return None, False
        pos, dir = self.find_closest(positions, self.player1_pos, self.player1_distances, self.player1_paths)
        if dir is None or pos is None:
            return None, False

        if are_adjacent(self.player1_pos, pos):
            if self.print: print(f'{name}_use_1')
            self.queue_use1(dir)
            return pos, True
        else:
            if self.print: print(f'{name}_move_1')
            self.queue_move1(dir)
            return pos, False

    def use_closest_2(self, positions, name) -> tuple[tuple[int, int]|None, bool]:
        if not self.can_act2():
            return None, False
        pos, dir = self.find_closest(positions, self.player2_pos, self.player2_distances, self.player2_paths)
        if dir is None or pos is None:
            return None, False

        if are_adjacent(self.player2_pos, pos):
            if self.print: print(f'{name}_use_2')
            self.queue_use2(dir)
            return pos, True
        else:
            if self.print: print(f'{name}_move_2')
            self.queue_move2(dir)
            return pos, False

    def move_to_closest_1(self, positions, name):
        if not self.can_act1():
            return None

        pos, dir = self.find_closest(positions, self.player1_pos, self.player1_distances, self.player1_paths)
        if dir is None or pos is None:
            return None

        if self.print: print(f'{name}_move_1')
        self.queue_move1(dir)
        return pos

    def move_to_closest_2(self, positions, name):
        if not self.can_act2():
            return None

        pos, dir = self.find_closest(positions, self.player2_pos, self.player2_distances, self.player2_paths)
        if dir is None or pos is None:
            return None

        if self.print: print(f'{name}_move_2')
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
            if self.print: print(f'Reset 1 {self.remain_on_plate_counter_1=}')
            self.remain_on_plate_counter_1 = 100

        if self.remain_on_plate_counter_1 > 0:
            self.remain_on_plate_counter_1 -= 1

        if self.plate_pos_2 is not None and self.player2_pos[0] == self.plate_pos_2[0] and self.player2_pos[1] == self.plate_pos_2[1]:
            if self.print: print(f'Reset 2 {self.remain_on_plate_counter_2=}')
            self.remain_on_plate_counter_2 = 100

        if self.remain_on_plate_counter_2 > 0:
            self.remain_on_plate_counter_2 -= 1


    def level21_wait_at_plate_2(self) -> None:
        if self.can_act2():
            plates = np.argwhere((self.map == swoq_pb2.TILE_PRESSURE_PLATE_RED) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_GREEN) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_BLUE))
            if np.any(plates):
                plate_pos = self.move_to_closest_2(plates, 'plate')
                if plate_pos is not None:
                    self.plate_pos_2 = plate_pos
                    self.plate_color_2 = self.map[self.plate_pos_2]

    def level21_wait_at_plate_1(self) -> None:
        if self.can_act1():
            plates = np.argwhere((self.map == swoq_pb2.TILE_PRESSURE_PLATE_RED) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_GREEN) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_BLUE))
            if np.any(plates):
                plate_pos = self.move_to_closest_1(plates, 'plate')
                if plate_pos is not None:
                    self.plate_pos_1 = plate_pos
                    self.plate_color_1 = self.map[self.plate_pos_1]

    def level21_place_boulder(self) -> None:
        plates = np.argwhere((self.map == swoq_pb2.TILE_PRESSURE_PLATE_RED) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_GREEN) | (self.map == swoq_pb2.TILE_PRESSURE_PLATE_BLUE))
        if np.any(plates):
            if self.can_act1() and self.player1_inventory == swoq_pb2.INVENTORY_BOULDER:
                plate_pos, placed = self.use_closest_1(plates, 'plate_boulder')
                if plate_pos is not None:
                    self.plate_pos_1 = plate_pos
                    self.plate_color_1 = self.map[self.plate_pos_1]
                if placed:
                    self.plates_with_boulders.append(self.plate_pos_1)
            if self.can_act2() and self.player2_inventory == swoq_pb2.INVENTORY_BOULDER:
                plate_pos, placed = self.use_closest_2(plates, 'plate_boulder')
                if plate_pos is not None:
                    self.plate_pos_2 = plate_pos
                    self.plate_color_2 = self.map[self.plate_pos_2]
                if placed:
                    self.plates_with_boulders.append(self.plate_pos_2)


    def store_plate_door_positions(self) -> None:
        plates = np.argwhere(self.map == swoq_pb2.TILE_PRESSURE_PLATE_RED)
        doors = np.argwhere(self.map == swoq_pb2.TILE_DOOR_RED)
        if np.any(plates) and np.any(doors):
            for pos in doors:
                self.plate_door_positions.add(tuple(pos))

        plates = np.argwhere(self.map == swoq_pb2.TILE_PRESSURE_PLATE_GREEN)
        doors = np.argwhere(self.map == swoq_pb2.TILE_DOOR_GREEN)
        if np.any(plates) and np.any(doors):
            for pos in doors:
                self.plate_door_positions.add(tuple(pos))

        plates = np.argwhere(self.map == swoq_pb2.TILE_PRESSURE_PLATE_BLUE)
        doors = np.argwhere(self.map == swoq_pb2.TILE_DOOR_BLUE)
        if np.any(plates) and np.any(doors):
            for pos in doors:
                self.plate_door_positions.add(tuple(pos))
        