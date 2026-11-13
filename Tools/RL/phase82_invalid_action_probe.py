"""Prove an unavailable gameplay action aborts Training without AI fallback.

Start this Python process before launching the Phase82MlValidation Unity batch
probe with -tk-rl-training. The starting Red Castle has no Barracks, so
TrainWarrior must be rejected by the authoritative action router.
"""

from __future__ import annotations

import numpy as np
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.exception import UnityCommunicatorStoppedException


def main() -> None:
    env = UnityEnvironment(file_name=None, base_port=5004, seed=778,
                           timeout_wait=120)
    try:
        env.reset()
        names = list(env.behavior_specs)
        assert len(names) == 1 and names[0].startswith("TinyKingdomStrategicRed")
        name = names[0]
        assert tuple(env.behavior_specs[name].action_spec.discrete_branches) == (19,)
        decision, terminal = env.get_steps(name)
        assert len(terminal) == 0 and len(decision) == 1
        env.set_actions(name, ActionTuple(
            discrete=np.full((1, 1), 2, dtype=np.int32)))
        disconnected = False
        for _ in range(3):
            try:
                env.step()
                _, terminal = env.get_steps(name)
                assert len(terminal) == 0, "Rejected action became terminal match"
            except UnityCommunicatorStoppedException:
                disconnected = True
                break
        assert disconnected, "Unity continued after rejected training action"
        print("PHASE_82_INVALID_ACTION_ABORT_PASS: TrainWarrior without Barracks "
              "stopped communicator without a match terminal", flush=True)
    finally:
        env.close()


if __name__ == "__main__":
    main()
