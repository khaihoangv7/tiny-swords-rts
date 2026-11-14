"""Train tabular Q-learning against the Rule-Based Commander."""

from __future__ import annotations

import argparse
from collections import Counter
import json
import math
import os
from pathlib import Path
import random
import subprocess
import tempfile
import time


ACTION_COUNT = 19
# IDs mirror RlAction in Assets/Scripts/AI/RlActionRouter.cs.
ACTION_NAMES = (
    'Wait', 'TrainPawn', 'TrainWarrior', 'TrainArcher', 'TrainLancer', 'TrainMonk',
    'BuildHouse', 'BuildBarracks', 'BuildArchery', 'BuildTower', 'BuildMonastery',
    'PrioritizeFood', 'PrioritizeWood', 'PrioritizeGold', 'Scout', 'Attack',
    'Defend', 'Retreat', 'SecureGoldMine',
)
ACTION_GROUPS = {
    'ResourcePriority': (11, 12, 13),
    'ScoutAndWait': (14, 0),
    'DefendAndRetreat': (16, 17),
    'Attack': (15,),
    'Other': (*range(1, 11), 18),
}
TABLE_VERSION = 2
ROOT = Path(__file__).resolve().parents[2]
WORKER_TARGET = 6
REINFORCEMENT_TARGET = 8


def write_json(path: Path, value: dict) -> None:
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(value, allow_nan=False), encoding='utf-8')
    for attempt in range(10):
        try:
            os.replace(temporary, path)
            return
        except PermissionError:
            if attempt == 9:
                raise
            time.sleep(0.02)


class Learner:
    def __init__(self, seed: int, trace_decay: float = 0.0) -> None:
        self.rng = random.Random(seed)
        self.trace_decay = trace_decay
        self.rows: dict[str, list[float]] = {}
        self.episodes = 0
        self.previous: tuple[str, int] | None = None
        self.traces: dict[tuple[str, int], float] = {}

    def break_trace(self) -> None:
        self.previous = None
        self.traces.clear()

    def update(self, reward: float, next_values: list[float]) -> None:
        if self.previous is None:
            return
        state, action = self.previous
        target = reward + 0.997 * max(next_values, default=0.0)
        error = target - self.rows[state][action]
        if not self.trace_decay:
            self.rows[state][action] += 0.20 * error
            return
        # Watkins Q(lambda) with replacing traces and greedy continuation.
        for key in tuple(self.traces):
            if key[0] == state:
                del self.traces[key]
        self.traces[self.previous] = 1.0
        for (visited_state, visited_action), eligibility in self.traces.items():
            self.rows[visited_state][visited_action] += 0.12 * error * eligibility

    def choose(self, state: str, legal: list[int], reward: float,
               exploration_hint: int | None = None) -> int:
        row = self.rows.setdefault(state, [0.0] * ACTION_COUNT)
        self.update(reward, [row[action] for action in legal])
        best = max(row[action] for action in legal)
        epsilon = max(0.08, 0.90 * 0.97 ** self.episodes)
        if self.rng.random() < epsilon:
            if exploration_hint in legal and self.rng.random() < 0.8:
                action = exploration_hint
            else:
                action = self.rng.choice(legal)
        else:
            action = self.rng.choice([action for action in legal if row[action] == best])
        if row[action] < best:
            self.traces.clear()
        else:
            decay = 0.997 * self.trace_decay
            self.traces = {key: trace * decay for key, trace in self.traces.items()
                           if trace * decay >= 0.01}
        self.previous = (state, action)
        return action

    def finish(self, reward: float) -> None:
        self.update(reward, [])
        self.break_trace()
        self.episodes += 1

    def evaluate(self, state: str, legal: list[int]) -> int:
        row = self.rows.get(state)
        if row is None:
            return self.rng.choice(legal)
        best = max(row[action] for action in legal)
        return self.rng.choice([action for action in legal if row[action] == best])

    def save(self, path: Path) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        write_json(path, {'version': TABLE_VERSION, 'actions': ACTION_COUNT, 'episodes': self.episodes,
                          'rows': [{'state': key, 'values': values}
                                   for key, values in sorted(self.rows.items())]})

    def load(self, path: Path) -> None:
        value = json.loads(path.read_text(encoding='utf-8'))
        if value['version'] != TABLE_VERSION or value['actions'] != ACTION_COUNT:
            raise ValueError('Incompatible Q-table schema. Start a v2 run without --resume.')
        rows = {row['state']: row['values'] for row in value['rows']}
        if any(len(row) != ACTION_COUNT or any(not math.isfinite(x) for x in row)
               for row in rows.values()):
            raise ValueError('Invalid Q-table values')
        self.rows = rows
        self.episodes = value['episodes']


def probe_action(observation: list[float], legal: list[int]) -> int:
    """Exercise construction, recruitment, and offensive orders before learning."""
    if len(legal) == 1:
        return legal[0]
    if observation[44] == 0 and observation[16] < 0.079 and 1 in legal:
        return 1
    if observation[44] == 0 and observation[16] < 0.079 and observation[5] > 0:
        return 0
    if observation[44] == 0 and 7 in legal:
        return 7
    if observation[20] + observation[28] < 0.079 and observation[5] < 0.04:
        for action in (2, 4):
            if action in legal:
                return action
    if observation[20] + observation[28] > 0:
        if 15 in legal or 14 in legal:
            return 15 if 15 in legal else 14
    gather = 11 if observation[0] < observation[2] else 13
    return gather if gather in legal else 0


def guided_action(observation: list[float], legal: list[int], state: str, map_id: int = 2) -> int:
    """Suggest a legal action for the current recruiting or attack phase."""
    if len(legal) == 1:
        return legal[0]
    has_barracks = observation[44] > 0
    workers = round(50 * observation[16])
    composition = {action: round(50 * observation[index])
                   for action, index in ((2, 20), (3, 24), (4, 28))}
    army = sum(composition.values())
    reserved = round(50 * observation[5])
    free_population = round(50 * observation[6])
    _, committed, shortage_action = state.rsplit('-', 2)
    shortage = int(shortage_action)
    if workers < 4 and 1 in legal:
        return 1
    if 15 in legal and committed == '0':
        return 15
    if shortage and shortage in legal:
        return shortage
    if not has_barracks:
        if 7 in legal:
            return 7
        if workers < WORKER_TARGET and 1 in legal:
            return 1
        return 12 if 12 in legal else 0
    if free_population <= 1 and 6 in legal:
        return 6
    if workers < WORKER_TARGET and army >= 2 and 1 in legal:
        return 1
    if army >= 2 and 8 in legal:
        return 8
    needs_recruits = (army + reserved < 12 and reserved < 3 if map_id == 3
                      else army < REINFORCEMENT_TARGET and reserved < 2)
    if needs_recruits:
        recruitment = [action for action in (2, 4, 3) if action in legal]
        if recruitment:
            if map_id == 3:
                weights = {2: 3, 3: 2, 4: 3}
                return min(recruitment, key=lambda action: composition[action] / weights[action])
            return min(recruitment, key=composition.__getitem__)
    if workers < WORKER_TARGET and 1 in legal:
        return 1
    if 15 in legal:
        return 15
    if army >= 2 and 14 in legal:
        if 18 in legal:
            return 18
        return 14
    gather = 11 if observation[0] < observation[1] else 12
    return gather if gather in legal else 0


class IslandGuidance:
    """Constrain optional Python decisions using only legal Goblin Island observations."""

    def __init__(self) -> None:
        self.last_food: int | None = None
        self.last_food_progress = 0.0
        self.next_food_order = 0.0
        self.next_food_scout = 0.0
        self.food_orders = 0
        self.food_scouts = 0
        self.mine_orders_filtered = 0

    def candidates(self, observation: list[float], legal: list[int], state: str) -> list[int]:
        now = observation[8] * 1800
        food = round(observation[0] * 1000)
        if self.last_food is None or food > self.last_food or food >= 120:
            self.last_food_progress = now
        self.last_food = food

        if len(legal) == 1:
            return legal
        _, committed, _ = state.rsplit('-', 2)
        if committed == '0' and 15 in legal:
            return legal

        # Unity already checks the minimum healthy force. Retain an extra
        # margin for the optional mine operation when goblins are known.
        if 18 in legal and round(observation[14] * 20) > 0:
            combatants = round(50 * (observation[20] + observation[24] + observation[28]))
            if combatants < 8:
                legal = [action for action in legal if action != 18]
                self.mine_orders_filtered += 1

        workers = round(observation[16] * 50)
        if food >= 120 or workers == 0:
            return legal
        if (committed == '0' and workers >= 4 and 14 in legal
                and now - self.last_food_progress >= 36 and now >= self.next_food_scout):
            self.next_food_scout = now + 36
            self.food_scouts += 1
            return [14]
        if 11 in legal and now >= self.next_food_order:
            self.next_food_order = now + 10
            self.food_orders += 1
            return [11]
        return legal


def write_eda(folder: Path, args: argparse.Namespace, episodes: list[dict]) -> None:
    """Summarize completed matches using Unity's processed-action counts."""
    totals = [0] * ACTION_COUNT
    accepted = [0] * ACTION_COUNT
    for episode in episodes:
        for index, name in enumerate(ACTION_NAMES):
            totals[index] += episode['actions'][name]['count']
            accepted[index] += episode['actions'][name]['accepted']
    decisions = sum(totals)
    groups = {name: sum(totals[index] for index in indices)
              for name, indices in ACTION_GROUPS.items()}
    outcomes = Counter(episode['result'] for episode in episodes)
    write_json(folder / 'eda_summary.json', {
        'map': args.map,
        'mode': 'evaluate' if args.evaluate else 'train',
        'start_seed': args.seed,
        'requested_episodes': args.episodes,
        'completed_episodes': len(episodes),
        'episode_limit_seconds': args.limit,
        'initial_speed': args.speed if args.speed is not None else (1 if args.watch else 10),
        'resume': args.resume,
        'shaping': args.shaping,
        'guided_exploration': args.guided_exploration,
        'island_guidance': args.island_guidance,
        'cohesion_shaping': args.cohesion_shaping,
        'outcomes': {result: outcomes[result] for result in ('win', 'loss', 'timeout')},
        'win_percent': round(100 * outcomes['win'] / len(episodes), 2),
        'decisions': decisions,
        'accepted': sum(accepted),
        'rejected': decisions - sum(accepted),
        'groups': {name: {'count': count, 'percent': round(100 * count / decisions, 2)
                          if decisions else 0} for name, count in groups.items()},
        'actions': {
            name: {'count': totals[index], 'accepted': accepted[index],
                   'rejected': totals[index] - accepted[index],
                   'percent': round(100 * totals[index] / decisions, 2) if decisions else 0}
            for index, name in enumerate(ACTION_NAMES)
        },
        'military_trained': sum(episode['military_trained'] for episode in episodes),
        'episodes': episodes,
    })


def read_eda_match(folder: Path, episode: int, result: str, episode_reward: float) -> dict:
    log_path = folder / 'matches.log'
    # Unity's MatchEnded callbacks send the terminal request and write match metrics.
    # The request can reach Python before the log append is complete.
    deadline = time.monotonic() + 5
    while True:
        try:
            lines = log_path.read_text(encoding='utf-8').splitlines(keepends=True)
        except (FileNotFoundError, PermissionError):
            lines = []
        matches = [line for line in lines if line.startswith('QL_RESULT ') and line.endswith('\n')]
        if len(matches) >= episode:
            break
        if time.monotonic() >= deadline:
            raise RuntimeError(f'EDA metrics missing for episode {episode}; see {log_path}')
        time.sleep(0.02)

    fields = dict(field.split('=', 1) for field in matches[episode - 1].split()[1:])
    if not {'seed', 'blueActionCounts', 'blueAcceptedActionCounts'} <= fields.keys():
        raise RuntimeError(f'Unity did not record EDA metrics for episode {episode}; see {log_path}')
    expected_result = {'win': 'BlueVictory', 'loss': 'RedVictory', 'timeout': 'Timeout'}[result]
    counts = [int(value) for value in fields['blueActionCounts'].split(',')]
    accepted = [int(value) for value in fields['blueAcceptedActionCounts'].split(',')]
    if (fields['result'] != expected_result or len(counts) != ACTION_COUNT
            or len(accepted) != ACTION_COUNT or sum(counts) != int(fields['blueOrders'])
            or sum(counts) - sum(accepted) != int(fields['blueRejected'])
            or any(not 0 <= ok <= count for count, ok in zip(counts, accepted))):
        raise ValueError(f'Inconsistent EDA metrics for episode {episode}; see {log_path}')
    return {
        'episode': episode,
        'seed': int(fields['seed']),
        'result': result,
        'seconds': float(fields['seconds']),
        'episode_reward': round(episode_reward, 6),
        'military_trained': int(fields['blueMilitary']),
        'workers_trained': int(fields['bluePawnsTrained']),
        'first_attack_seconds': (float(fields['blueFirstAttack'])
                                 if float(fields['blueFirstAttack']) >= 0 else None),
        'actions': {name: {'count': counts[index], 'accepted': accepted[index]}
                    for index, name in enumerate(ACTION_NAMES)},
    }


def exchange(folder: Path, process: subprocess.Popen, args: argparse.Namespace) -> None:
    learner = Learner(args.seed, trace_decay=0.85 if args.map == 3 else 0.0)
    if args.resume or args.evaluate:
        learner.load(args.output)
    completed = 0
    sequence = 0
    episode_reward = 0.0
    eda_episodes: list[dict] = []
    island = IslandGuidance() if args.island_guidance else None
    last_activity = time.monotonic()
    while completed < args.episodes:
        if process.poll() is not None:
            raise RuntimeError(f'Unity exited with code {process.returncode}; see {folder / "unity.log"}')
        if time.monotonic() - last_activity > 180:
            raise TimeoutError(f'No new request from Unity; see {folder / "unity.log"}')
        request_path = folder / f'request-{sequence + 1}.json'
        try:
            request = json.loads(request_path.read_text(encoding='utf-8'))
        except (FileNotFoundError, PermissionError):
            time.sleep(0.02)
            continue
        if request['sequence'] != sequence + 1:
            raise ValueError('Unexpected request sequence from Unity')
        if request['processed'] != sequence:
            learner.break_trace()
        sequence = request['sequence']
        last_activity = time.monotonic()
        response = {'sequence': sequence, 'action': 0, 'stop': False}
        reward = float(request['reward'])
        if not math.isfinite(reward):
            raise ValueError('Non-finite reward from Unity')
        episode_reward += reward
        if request['terminal']:
            completed += 1
            if not args.probe and not args.evaluate:
                learner.finish(reward)
                learner.save(args.output)
            if args.eda:
                eda_episodes.append(read_eda_match(folder, completed, request['result'], episode_reward))
                write_eda(folder, args, eda_episodes)
            print(f'episode={completed} result={request["result"]} '
                  f'final_reward={reward:g} episode_reward={episode_reward:.3f} '
                  f'states={len(learner.rows)}', flush=True)
            if island is not None:
                print(f'  food_priority={island.food_orders} scout_when_hungry={island.food_scouts} '
                      f'mine_filtered_decisions={island.mine_orders_filtered}', flush=True)
                island = IslandGuidance()
            episode_reward = 0.0
            response['stop'] = completed >= args.episodes
        else:
            legal = request['legal']
            observation = request['observation']
            if not legal or len(observation) != 96 or any(not 0 <= a < ACTION_COUNT for a in legal):
                raise ValueError('Invalid observation or action mask from Unity')
            state = request['state']
            if island is not None:
                legal = island.candidates(observation, legal, state)
            if args.probe:
                response['action'] = probe_action(observation, legal)
            elif args.evaluate:
                response['action'] = learner.evaluate(state, legal)
            else:
                hint = guided_action(observation, legal, state, args.map) if args.guided_exploration else None
                response['action'] = learner.choose(state, legal, reward, hint)
        write_json(folder / f'response-{sequence}.json', response)
        request_path.unlink()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity', type=Path, required=True)
    parser.add_argument('--map', type=int, choices=(2, 3), default=2,
                        help='2: Straight Lane (default), 3: Goblin Island')
    parser.add_argument('--episodes', type=int, default=10)
    parser.add_argument('--limit', type=int, default=1800, help='episode simulation seconds')
    parser.add_argument('--seed', type=int, default=9321)
    parser.add_argument('--output', type=Path, help='Q-table path (defaults to a separate file per map)')
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument('--resume', action='store_true')
    modes.add_argument('--evaluate', action='store_true',
                       help='play using the saved Q-table without updating it')
    parser.add_argument('--work-dir', type=Path, help='parent directory for Unity and match logs')
    modes.add_argument('--probe', action='store_true', help='scripted production probe; writes no Q-table')
    parser.add_argument('--shaping', action='store_true',
                        help='reward victory and early development, penalize defeat, time and timeout')
    parser.add_argument('--cohesion-shaping', action='store_true',
                        help='map 3 shaping for grouped armies, isolated losses and Castle attacks')
    parser.add_argument('--guided-exploration', action='store_true',
                        help='bias exploration toward building and recruiting')
    parser.add_argument('--island-guidance', action='store_true',
                        help='map 3 only: enable distant foraging, prioritize food and delay guarded mine orders')
    parser.add_argument('--watch', action='store_true', help='show the Unity Game view')
    parser.add_argument('--eda', action='store_true',
                        help='save per-match action counts and an EDA summary in the run folder')
    parser.add_argument('--speed', type=int, choices=(1, 2, 5, 10),
                        help='initial game speed (default: 1 with --watch, 10 otherwise)')
    args = parser.parse_args()
    if args.output is None:
        filename = 'q_table_goblin_island.json' if args.map == 3 else 'q_table_v2.json'
        args.output = Path.home() / 'AppData/LocalLow/DefaultCompany/TinyKingdomRL' / filename
    if args.episodes < 1 or not 10 <= args.limit <= 1800:
        parser.error('episodes must be positive and limit must be between 10 and 1800')
    if (args.resume or args.evaluate) and not args.output.is_file():
        parser.error(f'Q-table missing: {args.output}')
    if args.evaluate and args.guided_exploration:
        parser.error('--evaluate uses saved Q-values without guided exploration')
    if args.island_guidance and (args.map != 3 or args.probe):
        parser.error('--island-guidance requires --map 3 and a Q-table run')
    if args.cohesion_shaping and (args.map != 3 or not args.shaping or args.probe):
        parser.error('--cohesion-shaping requires --map 3 --shaping and a Q-table run')
    if args.eda and args.probe:
        parser.error('--eda requires a Q-table run')
    if args.work_dir is not None:
        args.work_dir.mkdir(parents=True, exist_ok=True)
    folder = Path(tempfile.mkdtemp(prefix='tinykingdom-qlearning-', dir=args.work_dir)).resolve()
    print(f'Unity and match logs: {folder}', flush=True)
    if args.eda:
        print(f'EDA summary after each match: {folder / "eda_summary.json"}', flush=True)
    command = [str(args.unity), '-projectPath', str(ROOT), '-force-d3d11',
               '-logFile', str(folder / 'unity.log'), '-executeMethod',
               'TinyKingdom.EditorTools.QlTrainingLauncher.Run',
               f'-tk-ql-train={folder}', f'-tk-ql-map={args.map}',
               f'-tk-ql-limit={args.limit}', f'-tk-rl-seed={args.seed}']
    if args.shaping:
        command.append('-tk-ql-shaping')
    if args.eda:
        command.append('-tk-ql-eda')
    if args.cohesion_shaping:
        command.append('-tk-ql-cohesion-shaping')
    if args.island_guidance:
        command.append('-tk-ql-island-guidance')
    if args.speed is not None:
        command.append(f'-tk-ql-speed={args.speed}')
    command.append('-tk-ql-watch' if args.watch else '-batchmode')
    flags = subprocess.CREATE_NO_WINDOW if os.name == 'nt' and not args.watch else 0
    process = subprocess.Popen(command, creationflags=flags)
    try:
        exchange(folder, process, args)
        if process.wait(timeout=45) != 0:
            raise RuntimeError(f'Unity exited with code {process.returncode}; see {folder / "unity.log"}')
        if args.probe:
            matches = (folder / 'matches.log').read_text(encoding='utf-8').splitlines()
            for match in matches:
                metrics = dict(field.split('=', 1) for field in match.split()[1:])
                if int(metrics['blueRecruits']) and int(metrics['blueMilitary']):
                    print('Production probe passed.', flush=True)
                    break
            else:
                raise RuntimeError('Probe did not confirm a Blue enlistment and military spawn')
        elif args.evaluate:
            print(f'Q-table used read-only: {args.output}', flush=True)
        else:
            print(f'Q-table: {args.output}', flush=True)
    finally:
        if process.poll() is None:
            process.terminate()
            process.wait(timeout=15)


if __name__ == '__main__':
    main()
