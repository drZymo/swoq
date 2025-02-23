import grpc
import swoq_pb2
import swoq_pb2_grpc
from time import sleep

_start_result_strings  = {
    swoq_pb2.START_RESULT_OK: 'OK',
    swoq_pb2.START_RESULT_INTERNAL_ERROR: 'INTERNAL_ERROR',
    swoq_pb2.START_RESULT_UNKNOWN_USER: 'UNKNOWN_USER',
    swoq_pb2.START_RESULT_USER_LEVEL_TOO_LOW: 'USER_LEVEL_TOO_LOW',
    swoq_pb2.START_RESULT_QUEST_QUEUED: 'QUEST_QUEUED',
}

_act_result_strings  = {
    swoq_pb2.ACT_RESULT_OK: 'OK',
    swoq_pb2.ACT_RESULT_INTERNAL_ERROR: 'INTERNAL_ERROR',
    swoq_pb2.ACT_RESULT_UNKNOWN_GAME_ID: 'UNKNOWN_GAME_ID',
    swoq_pb2.ACT_RESULT_MOVE_NOT_ALLOWED: 'MOVE_NOT_ALLOWED',
    swoq_pb2.ACT_RESULT_UNKNOWN_ACTION: 'UNKNOWN_ACTION',
    swoq_pb2.ACT_RESULT_GAME_FINISHED: 'GAME_FINISHED',
}


class GamePlayerStartException(BaseException):
    def __init__(self, result: swoq_pb2.StartResult):
        self.result = result


class GamePlayerActException(BaseException):
    def __init__(self, result: swoq_pb2.ActResult):
        self.result = result


class GamePlayer:
    def __init__(self, user_id:str):
        self.user_id = user_id
        self.channel = grpc.insecure_channel('localhost:5080')
        self.game_service = swoq_pb2_grpc.GameServiceStub(self.channel)


    def close(self) -> None:
        self.channel.close()


    def __enter__(self) -> object:
        return self


    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.close()


    def start(self, level:(int|None)=None) -> swoq_pb2.State:
        start_response = self.game_service.Start(swoq_pb2.StartRequest(userId=self.user_id, level=level))
        print(f'start({level=}): {_start_result_strings[start_response.result]}')

        while start_response.result == swoq_pb2.START_RESULT_QUEST_QUEUED:
            print('queued, waiting 1 sec...')
            sleep(1)
            start_response = self.game_service.Start(swoq_pb2.StartRequest(userId=self.user_id, level=level))
            print(f'start({level=}): {_start_result_strings[start_response.result]}')

        if start_response.result != swoq_pb2.START_RESULT_OK:
            raise GamePlayerStartException(start_response.result)
        
        self.game_id = start_response.gameId
        self.height = start_response.height
        self.width = start_response.width
        self.visibility_range = start_response.visibilityRange
        
        return start_response.state


    def act(self, action: swoq_pb2.DirectedAction) -> swoq_pb2.State:
        act_response = self.game_service.Act(swoq_pb2.ActionRequest(gameId=self.game_id, action=action))
        print(f'act({action=}): {_act_result_strings[act_response.result]}')

        if act_response.result != swoq_pb2.ACT_RESULT_OK:
            raise GamePlayerActException(act_response.result)

        return act_response.state
