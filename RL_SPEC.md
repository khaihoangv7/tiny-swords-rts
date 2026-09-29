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

## Phase 7B shared build and production actions

Phase 7B extends `FactionCommandService` with validated Build, resume
construction, Train and Rally actions. Rule-Based AI and a future RL Commander
must use these same calls. Rejections distinguish invalid ownership/actors,
unknown or invalid placement, unreachable sites, insufficient resources,
population blocks, full queues and stopped match state without exposing hidden
blocker identity.

Pawn Economy, Scout and Builder roles are Commander-side action ownership. A
future RL policy may choose high-level build or production intent, while normal
gameplay still owns pathfinding, construction progress, payment, population
reservation, FIFO training, spawning and Rally movement. Phase 7B adds no RL
observations, rewards, ML-Agents dependency or training loop.

## Phase 7C.1 shared defensive combat action

Phase 7C.1 adds validated `FactionCommandService.TryAttack(actor, observedEntityId)`.
A future RL Commander must use the same API. The service resolves the target only
after the EntityId is present in the faction's current visible-unit observations
and TeamVision still confirms visibility. The Rule-Based military policy stores
only copied threat values and EntityId, never a hidden live target reference.

Army membership, base defense and regroup are strategic policy. Normal
`CombatController`, `UnitMovement`, A*, unit separation and Monk auto-heal remain
low-level gameplay. Phase 7C.1 adds no RL package, training loop, reward changes or
offensive RL action. Future defensive RL observations must use the same visible
threat snapshots and must not infer targets from hidden transforms or Fog visuals.

## Phase 7C.1A shared legal safety features

`StrategicSafetyMap` derives reusable risk cells from the same faction-observable
`FactionKnowledge` available to the Rule-Based Commander. A future RL Commander may
receive equivalent derived risk features, but cannot receive undiscovered building
positions or hidden-unit positions. Remembered Tower/Castle danger is allowed only
because it comes from frozen legal building snapshots; dynamic unit danger exists
only during current visibility.

The optional A* traversal policy remains low-level execution support. Future RL
chooses high-level intentions and may request a safe move or gather action; it does
not control waypoints or bypass command validation. Phase 7C.1A adds no RL package,
observation tensor, action space, reward or training loop.

## Phase 7C.2 shared offensive actions

Phase 7C.2 adds validated Commander `TryAttackMove` and visible-building
`TryAttackBuilding` actions. A future RL Commander may choose the same high-level
concepts: regroup, AttackMove toward a known objective and Attack a currently
visible target. It must use `FactionKnowledge` plus `FactionCommandService` and
receives no hidden GameObject, Transform, HP or destruction event.

A remembered building snapshot is a legal strategic destination only. Direct Attack
still requires a current TeamVision-backed observation. Low-level A*, group
destinations, target acquisition, combat timing and damage remain normal gameplay.
Phase 7C.2 adds no ML-Agents package, tensors, rewards or training loop. Retreat is
defined by the following Phase 7C.3 contract; multi-squad strategy remains deferred.

## Phase 7C.3 shared retreat action and future observations

Phase 7C.3 adds the shared validated `FactionCommandService.TryRetreatMove` action
and generic `CombatDirective.Retreat`. Rule-Based AI uses it now; a future RL
Commander may choose the same high-level Retreat intent without controlling paths,
waypoints, target acquisition or individual attacks.

Potential future observations include own effective Army strength, average HP,
campaign remaining-force ratio, staging cohesion and locally relevant currently
visible enemy strength. These values must use the same `FactionKnowledge` legality:
hidden enemy units contribute nothing, remote Scout observations outside the battle
radius contribute nothing, and hidden building live state is unavailable. Phase
7C.3 adds no ML-Agents package, tensor, reward, training loop or RL policy.
