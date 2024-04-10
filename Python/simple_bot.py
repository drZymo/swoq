import grpc
import player_pb2
import player_pb2_grpc
import numpy as np

def put(array:np.ndarray, address:np.ndarray, value):
    i = np.ravel_multi_index(address, array.shape)
    np.ravel(array)[i] = value
        
def get(array:np.ndarray, address:np.ndarray):
    i = np.ravel_multi_index(address, array.shape)
    return np.ravel(array)[i]


def convert_state(game_state, nr_dims, my_pos, dimensions):
    positions = [set() for _ in range(nr_dims)]
    
    for u in game_state.updatedCells:
        for d in range(nr_dims):
            positions[d].add(u.address[d])
    
    for d in range(nr_dims):
        positions[d] = np.array(sorted(positions[d]))
        
    shape = tuple([len(positions[d]) for d in range(nr_dims)])

    state = np.zeros(shape)
    
    for u in game_state.updatedCells:
        address = np.array(u.address)
        val = 0
        if u.player is not None and len(u.player) > 0:
            val = -1
        elif u.foodValue > 0:
            val = 1
        put(state, address, val)
    
    return state


class Game:
    def __init__(self, random_name:bool=True, name:str=None):
        self._credentials = grpc.ssl_channel_credentials(open('D:/Projects/localhost.pem', 'rb').read())
        
        player_name = 'RandomRalph'
        if random_name:
            random_id = ''.join([chr(ord('a') + np.random.choice(26)) for _ in range(4)])
            player_name += '_' + random_id
        if name is not None:
            player_name = name
        self.player_name = player_name
        print(f'player_name={self.player_name}')

        self._register()
        
    def _register(self):
        with grpc.secure_channel("localhost:7262", self._credentials) as channel:
            stub = player_pb2_grpc.PlayerHostStub(channel)
            registerResponse = stub.Register(player_pb2.RegisterRequest(playerName=self.player_name))
        self.player_id = registerResponse.playerIdentifier
        self.dimensions = np.array(registerResponse.dimensions)
        self.start_pos = np.array(registerResponse.startAddress)
        self.nr_dims = len(self.dimensions)
        self.my_pos = self.start_pos
        self.expected_my_pos = None
        
    def get_score(self):
        score = None
        nr_snakes = None
        count = 0
        with grpc.secure_channel("localhost:7262", self._credentials) as channel:
            stub = player_pb2_grpc.PlayerHostStub(channel)
            for e in stub.Subscribe(player_pb2.SubsribeRequest(playerIdentifier=self.player_id)):
                if e.playerScores:
                    for s in e.playerScores:
                        if s.playerName == self.player_name:
                            score = int(s.score)
                            nr_snakes = int(s.snakes)
                    break
                count += 1
                if count > 10:
                    print('get score failed')
                    break

        return score if nr_snakes is not None and nr_snakes > 0 else None


    def move(self, next_pos):
        move = player_pb2.Move(playerIdentifier=self.player_id, snakeName=self.player_name, nextLocation=next_pos)
        self.expected_my_pos = next_pos
        with grpc.secure_channel("localhost:7262", self._credentials) as channel:
            stub = player_pb2_grpc.PlayerHostStub(channel)
            stub.MakeMove(move)

    def sync_game_state(self, plot:bool=False):
        with grpc.secure_channel("localhost:7262", self._credentials) as channel:
            stub = player_pb2_grpc.PlayerHostStub(channel)
            response = stub.GetGameState(player_pb2.EmptyRequest())
            for c in response.updatedCells:
                if c.player == self.player_name:
                    if np.all(np.array(c.address) == self.expected_my_pos):
                        self.my_pos = self.expected_my_pos
                        self.expected_my_pos = None
        
        state = convert_state(response, self.nr_dims, self.my_pos, self.dimensions)
        if plot:
            global ax_state, img_state
            if img_state is None:
                img_state = ax_state.imshow(state)
            else:
                img_state.set_data(state)
            #ax_state.scatter(self.my_pos[1], self.my_pos[0], marker='X', color='black')        
        return state


def get_distances(state:np.ndarray, my_pos:np.ndarray, plot:bool=False):
    todo = [my_pos]

    distances = np.ones_like(state) * np.inf
    paths = np.zeros(state.shape, dtype=np.int32)
    
    put(distances, my_pos, 0)
    put(paths, my_pos, np.ravel_multi_index(my_pos, state.shape))
    
    while todo:
        current_pos = todo[0]
        current_dist = get(distances, current_pos)
        todo = todo[1:]
        
        for d in range(len(state.shape)):
            if current_pos[d] > 0:
                next_pos = current_pos.copy()
                next_pos[d] += -1
                
                next_state = get(state, next_pos)
                if next_state >= 0:
                    next_dist = get(distances, next_pos)
                    if current_dist + 1 < next_dist:
                        put(distances, next_pos, current_dist + 1)
                        put(paths, next_pos, np.ravel_multi_index(current_pos, state.shape))
                        todo.append(next_pos)
                
            if current_pos[d] < state.shape[d] - 1:
                next_pos = current_pos.copy()
                next_pos[d] += 1
                
                next_state = get(state, next_pos)
                if next_state >= 0:
                    next_dist = get(distances, next_pos)
                    if current_dist + 1 < next_dist:
                        put(distances, next_pos, current_dist + 1)
                        put(paths, next_pos, np.ravel_multi_index(current_pos, state.shape))
                        todo.append(next_pos)
    if plot:
        global ax_dist, img_dist
        if img_dist is None:
            img_dist = ax_dist.imshow(distances)
        else:
            img_dist.set_data(distances)
    return distances, paths

#get_distances(state, game.my_pos, plot=True)


def find_closest_food(state:np.ndarray, my_pos:np.ndarray, plot:bool=False):
    distances, paths = get_distances(state, my_pos, plot=plot)
    food_positions = np.argwhere(state > 0)
    if not food_positions.any():
        return None, None

    food_distances = [get(distances, p) for p in food_positions]
    if plot: print(f'{food_distances=}')
    I = np.argmin(food_distances)
    closest_food = food_positions[I]
    
    # find path towards food
    path = []
    pos = closest_food
    path.insert(0, pos)
    while not np.all(pos == my_pos):
        prev_pos = get(paths, pos)
        prev_pos = np.array(np.unravel_index(prev_pos, paths.shape))
        pos = prev_pos
        path.insert(0, pos)
    path = np.array(path)
    assert(np.all(path[0] == my_pos))
    assert(np.all(path[-1] == closest_food))
    
    return closest_food, path

#find_closest_food(state, game.my_pos, plot=True)


def get_move_towards_food(state:np.ndarray, my_pos:np.ndarray, plot:bool=False):
    closest_food, path = find_closest_food(state, my_pos, plot=plot)
    if closest_food is None: return None

    next_pos = path[1]
    if plot: print(f'{next_pos=}')
    return next_pos

#get_move_towards_food(state, game.my_pos, plot=True)


def move_towards_food(game:Game, plot:bool=False):
    state = game.sync_game_state(plot=plot)
    
    next_pos = get_move_towards_food(state, game.my_pos, plot=plot)
    if next_pos is not None:
        if plot:
            print(f'move({next_pos=})')
        game.move(next_pos)

    score = game.get_score()
    if plot:
        print(f'{score=}')

    return score


game = Game()
score = game.get_score()
while True:
    move_towards_food(game)


