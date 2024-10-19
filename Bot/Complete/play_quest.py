from play import GamePlayer
import numpy as np

user_ids = ['6616b1c5bd0a697480a68319', '663d47788054476b438b61f4', '66ae2054d052c6450c7b989a']


def quest(user_id:str) -> None:
    with GamePlayer(user_id=user_id, plot=False, print=False) as player:
        player.start()
        while not player.finished:
            if np.random.uniform() < 0.05: # 1 in 20 steps random
                player.step_randomly()
            else:
                player.step()


def main() -> None:
    while True:
        user_id = np.random.choice(user_ids)
        quest(user_id)


if __name__ == '__main__':
    main()
