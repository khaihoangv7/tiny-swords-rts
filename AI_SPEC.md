# Tiny Kingdom RL - Rule-Based Commander Specification

## Phase 7A scope

Phase 7A enables one `RuleBasedCommander` for Red. Blue remains human-controlled.
The Commander manages only worker gathering and one exploration scout. Building,
production, military strategy, Gold Mine contest strategy, Goblin AI and
Reinforcement Learning are deferred.

## Architecture

Decision flow is:

`FactionKnowledge -> RuleBasedCommander policies -> FactionCommandService -> gameplay systems`

`ICommander` is the shared Commander boundary. `CommanderDecisionContext` passes
the issuing faction, legal knowledge, command authority and current gameplay time.
`EconomyCommanderPolicy` and `ScoutingCommanderPolicy` retain their own stable
assignments instead of mixing all decisions into one MonoBehaviour.

The default think interval is 0.50 seconds. No policy runs every frame. The hot
decision path uses the existing unit, resource and vision-source registries and
reuses policy buffers. It does not call `FindObjectsByType` or use LINQ.

## Legal faction knowledge

A faction has authoritative access to all of its own state, including its wallet,
population, units, buildings, HP, Pawn cargo and current orders.

Enemy and neutral observations follow these rules:

- Enemy units are exposed only while currently Visible. The visible-unit buffer is
  cleared and rebuilt each knowledge refresh. No last-known unit position or hidden
  `UnitController` reference is retained.
- Visible enemy buildings are copied into value observations.
- Explored enemy buildings use copied last-seen values from `FogMemorySystem`:
  entity ID, faction, type, position, footprint, completion, construction progress
  and last-seen health ratio. They contain no hidden live object reference.
- Visible resources may expose the current `ResourceNode` because they are current
  observations. Remembered resources are value snapshots containing ID, type,
  last-seen position/cell and last-seen remaining/maximum amount.
- Unexplored objects provide no observation.
- A hidden remembered resource is approached through its last-seen adjacent explored
  cell. Its hidden live state is not queried. Gathering is issued only after the
  node becomes Visible again.

Authoritative A* may route around physical blockers in unknown space, as it does
for a human Move command. The Commander receives only Accepted/RejectedUnreachable,
never a hidden object identity.

## Command authority

`FactionCommandService` currently implements `TryMove` and `TryGather`. It rejects:

- a command outside `MatchState.Playing`;
- a null, dead or non-world actor;
- an actor owned by another faction;
- a wrong actor type;
- an invalid or unobserved target;
- an unreachable destination/interaction.

Accepted Move uses `UnitMovement`; accepted Gather uses `PawnGatherer`. The service
never changes transforms, HP, wallets, population or resource amounts directly.
Future human, rule-based and RL command sources should share this authority rather
than simulate pointer input.

## Economy policy

The default target ratios are Food 40%, Wood 35% and Gold 25%. Deterministic small
rosters use 1/1/1 for three workers and 2/1/1 for four. A dedicated Pawn scout is
excluded from the worker count.

Valid active gather assignments remain stable. A worker is reconsidered only after
death, scout reservation, an invalid/depleted job, an unexpected Idle state or a
failed remembered-resource approach. Visible targets are selected by validity,
distance and stable EntityId. Failed targets receive a four-second retry cooldown.
The existing interaction slots, gather cycle, cargo, Castle drop-off and same-type
resource chaining remain authoritative.

## Scouting policy

Scout priority is Lancer, Warrior, Archer, then Pawn. Monk is excluded. Only one
available unit is reserved. The destination is an explored, walkable frontier cell
adjacent to Unexplored cells. Candidates prefer greater unexplored adjacency, then
shorter distance, then stable grid order.

The destination remains until reached, invalid, unreachable or no longer useful.
Rejected cells receive a five-second cooldown. The policy never reads hidden enemy
positions. When no frontier remains, the scout role is released; a Pawn can return
to economy work.

## Match lifecycle and debugging

The Commander issues commands only in `MatchState.Playing`. Scene reload naturally
recreates knowledge, worker assignments, scout state and cooldowns. Optional runtime
Inspector counters and the scout gizmo are disabled by default. Profiling uses the
`TinyKingdom.AI.RuleBasedCommander.Think` marker.

## Future phases

Phase 7B may add construction and production policies. Phase 7C may add military
strategy. A future `RLCommander` must receive the same legal `FactionKnowledge` and
use the same `FactionCommandService`; it receives no hidden-information exception.
