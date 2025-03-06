import grpc
import swoq_pb2
import swoq_pb2_grpc
import json
import urllib.parse
from time import sleep
from pathlib import Path
from datetime import datetime

env = json.loads(Path('env.json').read_text())

class ReplayFile:
    def __init__(self, user_name:str, start_request:swoq_pb2.StartRequest, start_response:swoq_pb2.StartResponse):
        sanitized_user_name = urllib.parse.quote(user_name)
        dateTimeStr = datetime.now().strftime('%Y%m%d-%H%M%S')
        filename = Path() / 'Replays' / f'{sanitized_user_name} - {dateTimeStr} - {start_response.gameId}.swoq'

        if not filename.parent.exists():
            filename.parent.mkdir(parents=True)

        self.file = open(filename, 'wb')

        header = swoq_pb2.ReplayHeader(userName = user_name, dateTime = datetime.now().isoformat())
        self._write_delimited(header)

        self._write_delimited(start_request)
        self._write_delimited(start_response)

    def close(self) -> None:
        self.file.close()

    def __enter__(self) -> object:
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.close()

    def append(self, act_request:swoq_pb2.ActionRequest, act_response:swoq_pb2.ActionResponse) -> None:
        self._write_delimited(act_request)
        self._write_delimited(act_response)

    def _write_delimited(self, message) -> None:
        bytes = message.SerializeToString()
        self._write_varint(len(bytes))
        self.file.write(bytes)

    def _write_varint(self, value:int) -> None:
        rem = value
        bytes = bytearray()
        while rem > 0:
            if rem > 128:
                b = 0x80 | rem & 0x7f
                rem >>= 7
            else:
                b = rem
                rem -= rem
            bytes.append(b)
        self.file.write(bytes)


class GameException(BaseException):
    pass


class Game:
    def __init__(self, game_service:swoq_pb2_grpc.GameServiceStub, response:swoq_pb2.StartResponse, replay_file:ReplayFile):
        self.game_service = game_service
        self.replay_file = replay_file

        self.game_id = response.gameId
        self.map_height = response.height
        self.map_width = response.width
        self.visibility_range = response.visibilityRange
        self.state = response.state

    def close(self) -> None:
        self.replay_file.close()

    def __enter__(self) -> object:
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.close()

    def act(self, action:swoq_pb2.DirectedAction) -> None:
        request = swoq_pb2.ActionRequest(gameId=self.game_id, action=action)
        response = self.game_service.Act(request)

        self.replay_file.append(request, response)

        if response.result != swoq_pb2.ACT_RESULT_OK:
            raise GameException(f'Act failed (result {response.result})')

        self.state = response.state


class GameConnection:
    def __init__(self):
        self.channel = grpc.insecure_channel(env['Host'] + ':5080')
        self.game_service = swoq_pb2_grpc.GameServiceStub(self.channel)

    def close(self) -> None:
        self.channel.close()

    def __enter__(self) -> object:
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.close()

    def start(self, level:(int|None)=None) -> Game:
        request = swoq_pb2.StartRequest(userId=env['UserId'], level=level)

        response:swoq_pb2.StartResponse = None
        while True:
            response = self.game_service.Start(request)

            if response.result == swoq_pb2.START_RESULT_OK:
                break

            if response.result == swoq_pb2.START_RESULT_QUEST_QUEUED:
                print('Quest queued, waiting 2 seconds before retrying ...')
                sleep(2)
                continue

            raise GameException(f'Start failed (result {response.result})')

        replay_file = ReplayFile(env['UserName'], request, response)

        return Game(self.game_service, response, replay_file)
