# Tiny Kingdom RL - Reinforcement Learning Specification

## Purpose

The RL agent acts as a strategic Commander.

It does NOT directly control individual units.

---

## Low-level control

Handled by normal game systems:

- movement
- A* pathfinding
- target selection
- attacking
- gathering
- returning resources
- building

---

## RL Observation

The future RL agent may observe:

- Food
- Wood
- Gold

- Population
- Population Cap

- Pawn count
- Warrior count
- Archer count
- Lancer count

- Enemy Warrior count
- Enemy Archer count
- Enemy Lancer count

- Own Castle HP
- Enemy Castle HP

- Own army strength
- Enemy army strength

- game time

---

## RL Actions

Possible strategic actions:

- TrainPawn
- TrainWarrior
- TrainArcher
- TrainLancer

- BuildHouse
- BuildBarracks
- BuildTower

- PrioritizeFood
- PrioritizeWood
- PrioritizeGold

- Attack
- Defend
- Retreat

- Wait

---

## Reward

Initial reward concept:

Win:
+100

Lose:
-100

Destroy enemy unit:
+5

Lose unit:
-3

Destroy enemy building:
+15

Lose building:
-15

Damage enemy Castle:
positive proportional reward

Own Castle damage:
negative proportional reward

Small time penalty:
discourage endless games.

These values are experimental and may be tuned later.

---

## Observable State and World Truth

Future Commander observations should use faction-observable state rather than
unrestricted world truth unless an experiment explicitly selects omniscient
observations. `TeamVisionSystem` is the gameplay authority for normal faction
awareness. A unit's local retaliation awareness after direct damage is not a
team reveal and must not be promoted into the Commander's global observation.

Phase 6B adds no RL observations, rewards or actions.

## Phase 6C.1 Faction Knowledge Contract

Future Rule-Based and RL Commanders must query knowledge for their own faction.
Current world truth is available only for objects in that faction's `Visible`
cells. `Explored` building and resource observations may use immutable
last-seen snapshots from `FogMemorySystem`; `Unexplored` cells provide no object
knowledge. Enemy units have no persistent last-seen observation.

Snapshot queries must not be converted back into hidden live scene references.
Hidden HP, repairs, construction, garrison, production, movement, destruction
and resource depletion are not observable until the faction sees the area
again. Physical A* navigation may still use authoritative occupancy internally,
but Commander observations and action validation must not expose that hidden
world state. Phase 6C.1 adds no AI/RL actions, rewards or policies.

Phase 6C.2 exposes Monk heal and Gold Mine entry/ejection as gameplay authority
APIs used by human input. Future Commanders may call those APIs but receive no
new policy, reward or direct unit-motion control in this phase. Gold Mine and
Goblin observations must follow the same faction knowledge contract as the
human player.

## Phase 7A shared Commander contract

Phase 7A introduces `ICommander`, `CommanderDecisionContext`, `FactionKnowledge`
and `FactionCommandService`. The Rule-Based Commander receives own authoritative
state, currently visible enemy units and copied last-seen building/resource values.
It receives no hidden live enemy reference. Its only current actions are validated
Move and Gather commands.

A future `RLCommander` must use the same knowledge legality and command authority.
RL does not get an omniscient observation path, direct transform/resource mutation
or mouse simulation by default. Invalid RL actions will receive the same structured
command rejection results. Phase 7A adds no ML-Agents package, reward function,
training loop or RL action policy.
