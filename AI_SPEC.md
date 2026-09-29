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
Inspector counters and optional scout/defense gizmos are disabled by default. Profiling uses the
`TinyKingdom.AI.CommanderTick` marker and fixed child markers for knowledge, roles,
building, production, scouting, military, economy, command dispatch and A*.

## Future phases

Phase 7B adds the construction and production policies documented below. Phase
7C.1 adds only the defensive military policy documented below; Phase 7C.2 retains
offensive strategy. A future `RLCommander` must receive the same legal
`FactionKnowledge` and use the same `FactionCommandService`; it receives no
hidden-information exception.

## Phase 7B building, population and production

Phase 7B adds separate `BuildingCommanderPolicy` and
`ProductionCommanderPolicy` instances coordinated by `RuleBasedCommander` at the
existing 0.50-second think interval. `CommanderPawnRoles` is the single
Commander-side owner of Pawn roles: Economy, Scout or Builder. Active Builder
work is protected from economy/scouting reassignment, Pawn scouts are protected
from construction selection, and released roles return to Economy after work
ends or becomes invalid.

The Commander uses `FactionCommandService.TryBuild`, `TryResumeConstruction`,
`TryTrain` and `TrySetRally`. These APIs validate match state, faction ownership,
observability, placement, reachability, resources, population and queue state,
then call the existing `BuildingSystem`, `PawnBuilder`, `UnitProductionQueue`
and `BuildingRallyPoint`. The Commander never edits wallets, population,
transforms, progress or production timers directly.

Only one strategic construction project is active at a time. Placement candidates
are generated deterministically in rings of radius 3 through 10 around Red's
Castle. Policy filtering uses explored terrain, Red's own buildings and Red's
known resource snapshots. It preserves a one-cell building gap, Castle drop-off
approach and known resource space. Authoritative world validation may reject a
cell because of an unknown blocker, but the Commander receives only a generic
placement rejection and advances to the next candidate.

Population policy maintains at least two effective free slots where possible,
using `Cap - Used - Reserved`. A House under construction counts as the active
project, preventing duplicates before its normal +5 cap bonus is applied.
Infrastructure is built gradually: Barracks from four Pawns, Archery from five,
and Monastery from six. Tower strategy, duplicate production buildings and base
expansion remain deferred.

Production targets six living-or-queued Pawns and eight non-Pawn military
population. Each producer has an AI-desired queue depth of one. Living and queued
units both count toward targets. The deterministic small-army sequence is Warrior,
Archer, Warrior, Lancer, Archer, Warrior, Monk, Lancer, with at most one Monk.
Existing scene units satisfy corresponding sequence slots. Costs, training time,
FIFO order and population reservations come only from `UnitCombatData` and
`UnitProductionQueue`.

Completed Barracks, Archery and Monastery share a deterministic staging region
selected from explored walkable cells near Red's Castle. Castle Pawn production
does not use the military Rally. Phase 7B issues no Attack, AttackMove, defense,
retreat, Tower strategy, Gold Mine contest or Goblin command.

### Phase 7B performance instrumentation and hotfix

`Tools > Tiny Kingdom > Profile Phase 7B Commander` records a 30-second normal
Profiler capture to `Logs/Phase7BPerformance.json`. It reports frame time,
Commander stage marker time, GC allocation and path requests split by gameplay,
Move, Gather, Build and Rally command sources. Counters are active only in the
Editor or an instrumentation-enabled build. Diagnostics do not log per tick.

Interaction-slot selection now ranks terrain-valid, unreserved cells by the same
deterministic priority before asking A* about them, then stops on the first
reachable result. AI construction no longer performs a separate reachability
preflight immediately before authoritative `BuildingSystem.TryBuild`. Pawn
drop-off discovery uses the live Castle registry instead of a scene-wide query.
These changes preserve authoritative movement and placement checks while avoiding
repeated path searches for candidates that cannot win the deterministic ranking.

## Phase 7C.1 defensive military Commander

Phase 7C.1 adds one `MilitaryCommanderPolicy` and one primary `ArmyGroup` for Red.
The group incrementally tracks living, world-participating Warrior, Archer, Lancer
and Monk units. Pawns, dead units, internal or mounted units and the active Phase 7A
Scout are excluded. A released Scout may join on a later Commander tick.

The group has four strategic states: `Forming`, `Ready`, `Defending` and
`Regrouping`. Forming and Regrouping reuse the Phase 7B military Rally staging cell
and distribute members over nearby valid navigation cells. Existing movement and
A* remain authoritative. Stable per-unit intents prevent the same Attack or Move
from being issued every 0.50-second decision tick.

The Red Castle footprint center is the base anchor. The provisional MVP
`BaseDefenseRadius` is 9 world units and `DefenseLeashRadius` is 12. Only hostile
playable-unit snapshots currently present in `FactionKnowledge.VisibleEnemyUnits`
can become strategic threats. `FactionKnowledge` copies visible combat activity
as value data: attacking Castle, another building, or a unit. It stores no target
reference. Priority is attacker of Castle, attacker of a building, attacker of an
own unit, other military, then Pawn; ties use squared distance to the Castle and
stable EntityId. The current target remains stable while visible and inside the
leash.

`FactionCommandService.TryAttack` accepts an observed EntityId, revalidates match
state, ownership, combat-capable unit type and current TeamVision, then calls the
normal `CombatController.Attack`. The policy never receives the live enemy object.
Warrior, Archer and Lancer focus the selected defensive threat. Monk receives only
normal Move orders to a safe support location and keeps its existing auto-heal;
it is never issued Attack.

When the target dies, leaves current Red vision or crosses the leash, the policy
selects another visible threat inside the base radius or enters Regrouping. A
normal Move to distributed staging cells clears the prior explicit pursuit. Once
survivors are inside the staging region, the group becomes Ready and waits. It
never selects remembered buildings, Blue Castle, resources or unexplored cells as
an objective and never performs an offensive AttackMove.

The policy stops with the existing Commander outside `MatchState.Playing`, and
Reset clears membership, target and intent caches. Fixed Profiler markers are
`TinyKingdom.AI.MilitaryPolicy`, `TinyKingdom.AI.ThreatEvaluation` and
`TinyKingdom.AI.ArmyRegroup`. Phase 7C.2 retains all offensive strategy, Gold Mine
strategy, strength-based retreat, multi-squad tactics and counter-production.

## Phase 7C.1A strategic safety hotfix

`StrategicSafetyMap` is a reusable AI decision aid rebuilt on the existing
0.50-second Commander tick only when relevant legal knowledge changes. It reuses
40x28 risk/exploration buffers and derives danger only from the Red Castle anchor,
currently visible enemy-unit value observations, and visible or remembered enemy
building snapshots. Hidden units contribute no danger after leaving vision;
undiscovered buildings never contribute danger.

Normal Red economy uses a provisional `HomeEconomicRadius` of 11 world units around
the Red Castle. A resource must be legally known, non-depleted according to current
or last-seen knowledge, inside this radius, on explored safe ground and reachable by
an explored safe route. The 40/35/25 ratios remain targets. If the desired type has
no eligible target, the worker tries other safe types by largest remaining target
deficit with stable enum ties, then remains available. AI-issued Gather jobs pass
the same eligibility and
traversal policy into Pawn same-type chaining; the original human Gather API remains
unchanged.

Tower and Castle exclusion radii use the remembered authoritative defensive range
plus a provisional 1.5-unit margin. Their surrounding three-unit base buffer is
dangerous to civilians and costly for scouts. Visible Warrior, Archer and Lancer
danger uses observed attack range plus a 2.5-unit reaction buffer. Overlapping
regions naturally represent visible groups. A threatened economy Pawn cancels the
unsafe job and retreats toward an explored safe cell near its Castle. A Builder is
interrupted only when its own construction site becomes dangerous.

Scout state is `Exploring` or `Retreating`. Frontier ranking is deterministic:
lower known risk, then nearer map perimeter, then greater unexplored-neighbor gain,
then travel distance and cell coordinates. Safe useful destinations are retained.
New danger invalidates an unsafe destination or route and selects an explored safe
retreat cell. Lancer, Warrior, Archer, Pawn priority and Monk exclusion remain.

Retreat uses a separate explored-cell traversal policy. It may cross the minimum
number of currently dangerous cells needed to leave an already-overlapping danger
zone, with very high Risky/Dangerous/Forbidden costs, while the retreat destination
must still be Safe. This prevents a Pawn or Scout already surrounded by a newly
revealed threat radius from freezing because every immediate neighbor is forbidden
for normal work/exploration.

The existing A* accepts an optional `IPathTraversalPolicy`. Default player and
gameplay calls pass no policy. Civilian AI forbids unexplored and severe-danger
cells; Scout AI adds costs around known risk and forbids known defensive cores.
No second pathfinder or offensive action is introduced. Fixed markers include
`TinyKingdom.AI.StrategicSafety`, `TinyKingdom.AI.SafeResourceSearch`,
`TinyKingdom.AI.ScoutRiskEvaluation`, `TinyKingdom.AI.ScoutRetreat` and
`TinyKingdom.AI.CivilianRetreat`.

## Phase 7C.2 offensive strategy

`MilitaryCommanderPolicy` extends the single Army Group with `PreparingAttack`,
`Advancing` and `Assaulting`. A normal operation requires six living available
military population and one legally known enemy building. Seventy-five percent of
the snapshotted operation members must assemble around the existing staging point.
The snapshot prevents newly produced units from streaming individually across the
map; they remain at Rally staging for the next operation.

`OffensiveTargetSnapshot` is value-only: EntityId, type, faction, frozen position
and category. Candidates come only from `FactionKnowledge.KnownEnemyBuildings`.
Priority is Tower, production building, other building and Castle, followed by
Army-profile risk, squared distance and EntityId. Selection performs no per-target
A*. Once selected, the objective is stable; later Scout discoveries do not redirect
the active operation.

Remembered buildings authorize AttackMove toward the frozen area. They never
authorize direct Attack. Only `FactionCommandService.TryAttackBuilding` may resolve
the live target, and only after current building observation plus TeamVision
validation. When a remembered area becomes Visible without that EntityId, the
objective is stale and clears. Completion, bounded route failure, Army loss or a
base-defense interrupt enters Regrouping and applies a short reevaluation delay.

Group advance uses distributed destinations and `TryAttackMove`, retaining one
intent per participant. Commands are reissued only for a state/objective change,
unexpected Idle/failure or a resumed advance after combat. Normal CombatController
owns acquisition, cooldowns, projectiles, melee slots and damage. The Army traversal
profile treats known defenses as costs rather than civilian forbidden cells. Monk
uses Move to a support point behind the group centroid; Scout remains separate.

Fixed markers are `TinyKingdom.AI.OffensiveEvaluation`,
`TinyKingdom.AI.StrategicTargetSelection`, `TinyKingdom.AI.OffensiveAdvance` and
`TinyKingdom.AI.OffensiveRegroup`. The Commander tick remains 0.50 seconds. Phase
7C.3 retains counter composition, multiple squads, tactical micro, advanced Monk
positioning, Gold Mine warfare and special Castle assault planning.

## Phase 7C.3 combat strength, retreat and recovery

The single Army Group adds `Retreating` and `Recovering`. The complete strategic
flow is Forming -> Ready -> PreparingAttack -> Advancing -> Assaulting, with
Defending able to interrupt legitimate base emergencies. A losing Advancing or
Assaulting campaign enters Retreating, completes its movement home, then enters
Recovering. Recovery returns to Ready for a later normal Phase 7C.2 evaluation;
it never launches a new offensive in the same evaluation.

`CombatStrengthTuning` uses provisional relative values: Warrior 1.00, Archer
0.75, Lancer 0.60 and Monk support 0.45. Effective unit strength is base value
times clamped current-HP ratio. Pawn is excluded. Visible Tower and Castle add
2.00 and 3.00 respectively, also health weighted. This preserves the current
balance fact that one Warrior is stronger than one Lancer while treating Monk as
support rather than direct damage.

Exact enemy strength is derived only from copied `FactionKnowledge` observations
inside a provisional nine-unit radius of the current campaign centroid. Hidden
units immediately contribute zero. A Scout seeing enemies elsewhere cannot affect
the main battle. Only currently visible Tower/Castle snapshots contribute exact
building strength; hidden remembered defenses remain route/target risk through
`StrategicSafetyMap` and never expose live HP or garrison state.

At the actual PreparingAttack -> Advancing transition, the policy records starting
campaign population, member count, effective strength and average HP. The current
casualty ratio uses only surviving members of that snapshot, so reinforcements at
Rally cannot improve it. Heavy casualties below 0.45 retreat immediately. Average
HP below 0.35 or relevant visible enemy strength above 1.40 times own strength must
persist for two Commander evaluations. Minor cohesion loss still uses local regroup.

Retreat clears offensive target and AttackMove intent without deleting legal
building memory. `FactionCommandService.TryRetreatMove` applies the generic
`CombatDirective.Retreat` and distributes survivors around the existing staging
point. The directive suppresses new passive acquisition, retaliation and long
chase while preserving normal damage received; Phase 7C.3 chooses movement priority
instead of self-defense micro. Commands are retained while movement progresses and
retried only after failure or unexpected Idle. Seventy-five percent of surviving
participants reaching staging completes retreat; a provisional 12-second timeout
prevents one stuck survivor from blocking recovery.

Recovering keeps economy, production, construction and Scout policies running.
Readiness requires at least six military population, 75 percent staged, at least
three seconds recovery time, and either average HP 0.65 or effective strength 4.0.
It does not require full HP or a living Monk, so fresh production can recover an
army without regeneration. A provisional ten-second recovery ceiling permits a
staged six-population Army to leave recovery even when no healing source exists,
preventing an injured no-Monk force from deadlocking forever. Units produced during
retreat remain at Rally and join only after survivors return. A recent
failed/retreated objective cools down for 12 seconds; another legal objective may
be chosen meanwhile.

An emergency near the Red base can interrupt Recovering immediately. A Retreating
army finishes the distant part of withdrawal first and may defend once at least
half of its survivors are back; after that defense it resumes recovery. Reset and
Match End clear campaign members, baselines, directives, hysteresis, cooldowns and
destinations. Fixed markers are `TinyKingdom.AI.CombatStrengthAssessment`,
`TinyKingdom.AI.RetreatEvaluation`, `TinyKingdom.AI.RetreatExecution` and
`TinyKingdom.AI.RecoveryEvaluation`.
