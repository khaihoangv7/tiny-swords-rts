"""Exercise the real ML-Agents communicator without running PPO training.

Start this process first. Then launch the Unity Editor in batch Play Mode with
Phase82MlValidation.RunBatch and -tk-rl-training. The Unity validator ends five
short episodes through the authoritative Blue Castle damage path.
"""

from __future__ import annotations

import time

import numpy as np
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment


BEHAVIOR = "TinyKingdomStrategicRed"
EXPECTED_EPISODES = 5


def main() -> None:
    env = UnityEnvironment(file_name=None, base_port=5004, seed=777,
                           timeout_wait=120)
    try:
        env.reset()
        names = list(env.behavior_specs)
        assert len(names) == 1 and names[0].startswith(BEHAVIOR), names
        name = names[0]
        spec = env.behavior_specs[name]
        assert len(spec.observation_specs) == 1
        assert spec.observation_specs[0].shape == (96,)
        assert tuple(spec.action_spec.discrete_branches) == (19,)
        assert spec.action_spec.continuous_size == 0
        print("PHASE_82_HANDSHAKE_PASS: behavior", name,
              "observation=96 discrete=(19,)", flush=True)

        deadline = time.monotonic() + 150
        episodes = 0
        decisions = 0
        choices = (0, 1, 12)  # Wait, TrainPawn, PrioritizeWood.
        while episodes < EXPECTED_EPISODES and time.monotonic() < deadline:
            decision, terminal = env.get_steps(name)
            for agent_id in terminal.agent_id:
                index = terminal.agent_id_to_index[agent_id]
                reward = float(terminal.reward[index])
                assert reward == 1.0, (episodes, reward)
                assert not bool(terminal.interrupted[index])
                episodes += 1
                print("PHASE_82_TERMINAL_PASS:", episodes,
                      "reward=", reward, flush=True)
            if episodes >= EXPECTED_EPISODES:
                break
            if len(decision) > 0:
                values = decision.obs[0]
                assert values.shape[1] == 96, values.shape
                assert np.isfinite(values).all()
                assert ((values >= 0) & (values <= 1)).all()
                if decisions == 0:
                    assert decision.action_mask is not None
                    mask = decision.action_mask[0][0]
                    assert mask.shape == (19,)
                    assert not mask[0], "Wait must remain available"
                    assert mask[2] and mask[3] and mask[4] and mask[5], (
                        "Military training needs a producer", mask)
                    print("PHASE_82_ACTION_MASK_PASS: starting military "
                          "actions unavailable without producers", flush=True)
                action = np.full((len(decision), 1),
                                 choices[decisions % len(choices)],
                                 dtype=np.int32)
                env.set_actions(name, ActionTuple(discrete=action))
                decisions += len(decision)
            env.step()
        assert episodes == EXPECTED_EPISODES, (episodes, decisions)
        print("PHASE_82_PYTHON_PROTOCOL_PASS: episodes=", episodes,
              "decisions=", decisions, flush=True)
    finally:
        env.close()


if __name__ == "__main__":
    main()
