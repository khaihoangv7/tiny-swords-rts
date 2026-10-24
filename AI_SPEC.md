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
The existing interaction slots, gather cycle, cargo, shared Castle/Tower drop-off
and same-type resource continuation remain authoritative. Phase 6H.1 lets the
core Pawn keep a stable assignment across visible slot overflow and depletion.
Hidden continuation uses only frozen Fog-memory positions; the AI safety policy
filters both visible nodes and remembered positions without receiving hidden live
resource state.

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

## Phase 7D final Rule-Based baseline

Phase 7D integrates the existing policies rather than replacing them. The legal
flow remains `FactionKnowledge -> policy -> FactionCommandService -> gameplay`.
Economy owns ordinary Pawn work and civilian withdrawal; Scouting owns one Scout;
Building owns one Builder/project; Production owns bounded queues and Rally;
Military owns the single ArmyGroup. Policy order reserves Builder and Scout roles
before Economy assigns remaining Pawns, while Military excludes the active Scout.
Urgent visible base defense can interrupt offense/withdrawal. Retreat and Recovery
finish before another campaign; routine economy and production continue alongside
those military states.

| Policy/service | Runtime responsibility |
|---|---|
| `EconomyCommanderPolicy` | Safe gathering, reassignment and civilian withdrawal |
| `ScoutingCommanderPolicy` | One legal frontier Scout and safe standby |
| `BuildingCommanderPolicy` | One deterministic project and Builder recovery |
| `ProductionCommanderPolicy` | Population-aware bounded queues and Rally |
| `MilitaryCommanderPolicy` | Defense, one ArmyGroup, offense, retreat and recovery |
| `StrategicSafetyMap` | Risk derived only from legal current/remembered knowledge |
| `FactionKnowledge` | Observation boundary |
| `FactionCommandService` | Ownership, match-state and observability command authority |

Conflict priority is enforced through match guards, role ownership and military
state transitions rather than one global switch:

| Priority | Intent |
|---:|---|
| 1 | Match End: all new Commander actions stop |
| 2 | Immediate visible base defense |
| 3 | Retreat |
| 4 | Recovery |
| 5 | Active offensive campaign |
| 6 | Army regroup/readiness |
| 7 | Scout exploration |
| 8 | Builder project |
| 9 | Normal economy assignment |

The final provisional configuration is:

| Setting | Value |
|---|---:|
| Commander think interval | 0.50 s |
| Worker target ratios | Food 40%, Wood 35%, Gold 25% |
| Home economic radius | 11 |
| Target Pawns | 6 |
| Target military population | 8 |
| Production queue depth | 1 |
| Monk cap | 1 |
| Minimum offensive population | 6 |
| Normal attack assembly | 0.75 |
| PreparingAttack timeout | 10 s |
| Timeout assembly fallback | 0.60 |
| Combat assessment radius | 9 |
| Strength: Warrior / Archer / Lancer / Monk | 1.00 / 0.75 / 0.60 / 0.45 |
| Visible Tower / Castle threat | 2.00 / 3.00 |
| Retreat remaining force / average HP | 0.45 / 0.35 |
| Retreat enemy strength ratio / hysteresis | 1.40 / 2 ticks |
| Retreat arrival / timeout | 0.75 / 12 s |
| Recovery minimum / maximum duration | 3 s / 10 s |
| Recovery average HP / effective strength | 0.65 / 4.0 |
| Recovery assembly | 0.75 |
| Failed objective cooldown | 12 s |

Normal staging now uses the configured 75% assembly threshold instead of requiring
every member, so one unreachable member cannot keep the Army in Forming forever.
PreparingAttack launches after normal assembly, may use the 60% fallback after ten
seconds, or fails the current objective cleanly when even that fallback is not
achievable. The target receives the existing finite cooldown and the Army regroups.
Completed exploration enters a stable exhausted state instead of repeatedly
reserving and releasing a Scout each Commander tick. Phase 7E.1 retains that
stability while keeping the official promoted Scout reserved for bounded stale
memory re-scout. Temporarily unsafe/cooldown-only frontiers are reconsidered on
the slower Scout target gate.

`CommanderProgressDiagnostics` records successful Gather assignment, Build/resume,
Train, Scout destination, Army state change, offensive launch, objective resolution,
resource income and last meaningful action timestamps at Commander frequency. It
does not issue commands or alter gameplay. The optional 25-second stall warning is
disabled by default and rate limited when enabled. The Phase 7D validator checks
wiring, economy/population, core building data, staging, tuning, role separation,
MatchManager and the absence of Goblin units. Full-match behavior and performance
still require Unity Play Mode and Profiler validation.

Intentional baseline limits remain one ArmyGroup, no flanking, focus fire, counter
production, forward bases, strategic Gold Mine warfare, advanced garrison strategy
or enemy-unit memory outside current vision. These are deferred and are not hidden
information shortcuts.

## Phase 7E.1 Adaptive Scout and Exploration 2.0

Red scouting uses the same authoritative Pawn promotion as the human player.
`ScoutingCommanderPolicy` requests `FactionCommandService.TryPromoteScout`; the
shared gameplay component validates a living faction-owned Pawn, spends exactly
50 Wood, enforces one active Scout, applies Pawn vision x2.5, owns the fire marker
and Fog-safe ambience, and clears registration on death. Red receives no free
Scout, AI-only vision, or military-unit fallback. Promotion requires at least
four Pawns and meaningful information need. It normally preserves Wood for the
currently planned unpaid building. If that reservation blocks promotion for
60 game seconds while at least 50 Wood is available, the Scout can use that
Wood to resume exploration. It prefers an idle low-disruption Economy Pawn;
the bounded busy-worker fallback is described below.

`CommanderPawnRoles` treats an active promoted Pawn as `Scout` before Builder or
Economy allocation. Economy and Building policies therefore cannot overwrite its
movement, and `ArmyGroup` plus Phase 6G.2 formation eligibility continue to reject
Pawn units. A dead Scout clears from the gameplay registry; a replacement is
considered only after the bounded promotion cooldown and only while frontier or
stale-memory value remains.

Frontier remains a walkable explored cell adjacent to Unexplored terrain. Candidate
generation reuses buffers, evenly samples at most 48 frontier cells for full
scoring, and runs only when the stable current target becomes invalid, is reached,
or the three-second reevaluation gate opens. The configurable score is:

`4.0 * sampled information gain + 1.5 * geographic novelty
- 0.18 * travel distance - 14.0 * StrategicSafetyMap risk`.

Information gain samples Unexplored cells inside the promoted Scout's real vision
radius without simulating Team Vision or running A* per candidate. Only the chosen
destination is submitted to normal pathfinding. Remembered Tower/Castle danger and
currently visible military danger come from `StrategicSafetyMap`; Unexplored cells
carry no automatic danger penalty. A nearby visible combat group or a dangerous
known position moves the Scout into bounded local evasion, followed by repositioning.

When frontier is exhausted the official Scout remains reserved and rechecks at a
slow interval instead of entering an assign/release loop. Buildings and resources
gain snapshot-only `LastSeenTime` metadata when legally visible. Hidden snapshot
age and strategic type determine re-scout value; the target stores only copied
`EntityId`, cells and scores. No hidden `GameObject`, `Transform`, HP, resource
amount or destruction state is queried. Re-observation through normal Team Vision
refreshes or removes the memory. Enemy Scout fire audio remains gated by camera and
the human faction's current visibility.

### Phase 7E.1 Scout intelligence hotfix

Scouting now separates exploration from reconnaissance. Before hostile presence is
known, a bounded top set of at most 48 frontiers favors the Scout's real information
gain, the size of the nearby unknown region and progressive depth away from its own
known home anchor. Depth is only a progression signal; it never predicts an opposing
spawn. A fixed eight-entry recent-target history applies a decaying regional penalty,
preventing repeated middle-map A/B oscillation without creating unbounded memory.

Any legally remembered hostile building or legally observed military group creates
an enemy-interest region. Buildings come only from Fog memory, and the military
summary is a value-only last-seen snapshot containing Warrior, Archer, Lancer and
Monk counts, observation time and observed centroid. It refreshes only from currently
visible enemies and remains frozen while they are hidden. Production policy does not
consume the summary in this phase.

Reconnaissance generates at most 40 deterministic perimeter candidates around known
Castle, production, Tower, House and military anchors. It never targets a Castle or
Tower center. Candidates use the promoted Pawn's authoritative 2.5x vision, strategic
value, unknown information, staleness, travel and known risk. Generic exploration
accepts only Safe destinations; recon may approach a Risky perimeter while normal
Scout traversal still forbids Dangerous defensive cores. Evasion accepts only an
explored Safe retreat and overrides both modes.

A recon target remains stable, dwells for 2.5 seconds after arrival, then enters an
18-second mission cooldown. Visible danger interrupts it and adds a 12-second re-entry
cooldown. Perimeter history rewards later observation from another side and prevents
immediate left/right ping-pong. All target state remains copied IDs, cells, scores and
timestamps; no hidden live enemy reference or hard-coded enemy spawn is retained.

### Phase 7E.1 full-match stability rules

Building legality remains centralized in `BuildingSystem` for both player and
Commander requests. Before accepting a footprint it performs an allocation-free,
bounded connectivity test over the footprint plus a six-cell margin. Candidate cells
are treated as blocked without spawning a site. A friendly world unit invalidates
the candidate only when its component reached surrounding traversable space before
the hypothetical block and becomes locally sealed afterward. The flood fill uses
the same walkability, occupancy, elevation and reciprocal stair connector rules as
navigation. Sheep retain their existing dedicated evacuation path.

An Economy-role Pawn that is idle with cargo is recovered through
`FactionCommandService.TryReturnResources`; Scout and manually controlled Pawns are
not globally commandeered. Drop-offs remain distance ranked, but each Castle/Tower
must reserve a reachable interaction slot. A failed or destroyed choice is released,
temporarily skipped and followed by the next candidate. No-drop-off retries use a
bounded 1/2/4/8 game-second backoff, while three seconds without physical progress
causes one recovery attempt. Resource type, safety filters and legal memory remain
active through deposit so equivalent-resource chaining resumes afterward.

The performance capture separates Commander Move, Scout Move, Gather, Build, Rally
and economy Return path requests; records command attempts and building candidate
evaluations; and samples audio pool size, GC and knowledge collection sizes. These
counters diagnose x10 behavior but do not replace the extended Play Mode Profiler run.
`FogWorldVisibility` also removes destroyed entity keys from its reusable presentation
gate cache during refresh, preventing spawn/death churn from accumulating stale entries.

### Phase 7E.1 Red construction reliability rules

Player and Commander construction converge on `BuildingSystem`, then `BuildingSite`
and `PawnBuilder`. Placement generates the same edge-adjacent work slots used at
runtime while the prospective footprint is blocked. A usable slot must be stable
ground at the footprint elevation, outside stairs/connector corridors, unoccupied,
and connected to external walkable space. At least one selected Builder must also
have an authoritative path to a slot before a site is created.

The site is the sole owner of Pawn-to-slot reservations. A Pawn reserves one concrete
reachable free slot before movement and keeps it until the assignment ends or bounded
recovery proves another slot is needed. Red assigns at most three concurrent Builders,
never more than the site's free slot capacity, and always preserves the configured
minimum Economy workers. Promoted Scouts cannot acquire the Builder role.

An already valid same-Pawn/same-site assignment is deduplicated without touching its
path, reservation or working state. Builders enter work within 0.2 world units and
remain working through small nudges up to 0.48 units. Separation remains enabled in
travel and is suppressed only near that Pawn's reserved final slot. A displaced Pawn
first makes bounded returns to its own slot, then may select a different free reachable
slot. Three game seconds without authoritative progress triggers a site-level recovery;
recovery has a two-second cooldown and reports an access failure rather than destroying
or refunding an impossible existing site.

Per-site and global counters record construction commands, deduplicated requests and
builder path requests. Optional site gizmos show invalid, free, reserved, approaching
and working slots. These diagnostics are disabled by default and the extended x10 Play
Mode profile remains the authority for command/path rates.

### Phase 7E.2 adaptive economy and economic outposts

`EconomicDemandModel` is the single short-horizon source for Red resource demand. It
reads the authoritative stockpile, unpaid building and production plans, Scout
promotion pressure and population forecast. Costs already paid by construction or a
production queue are never reserved a second time. Food, Wood and Gold deficits drive
worker targets; the former 40/35/25 ratios remain only a fallback for callers without
the model.

Economy planning runs at a slower four-second cadence than the Commander tick. A valid
gather command remains untouched. At most one empty-handed Economy Pawn changes
resource role per planning pass, with an eight-second dwell unless the target deficit
is critical. Builder and Scout roles remain excluded. Target scoring combines Pawn
travel, nearest completed friendly Castle/Tower drop-off, assignment crowding,
depletion and known civilian risk. Hidden resources use only frozen
`ResourceKnowledgeSnapshot` values and become live targets only after normal vision.

Known resource snapshots are grouped into at most sixteen clusters from at most 96
remembered nodes and refreshed no faster than every eight game seconds. The cache
contains only cells, positions, amounts and scores. A safe, sufficiently rich cluster
far from an existing drop-off may request one Tower economic outpost after core needs.
The normal `BuildingSystem`, payment, placement, trap prevention, construction slots
and Builder commands remain authoritative. Outposts keep a 45-second cooldown, never
overlap resources or existing structures, and never use hidden enemy state.

Population planning forecasts the next unpaid production request and prioritizes one
House when predicted headroom reaches the configured threshold. House cost remains the
authoritative 80 Wood; its Sheep is a bonus and never a reason to build a House. Builder
selection prefers idle, empty-handed Pawns and penalizes workers assigned to a current
resource deficit. After construction, normal role synchronization returns Builders to
adaptive Economy allocation.

### Phase 7E.3 reactive production and counter-composition

Red production combines a stable mixed baseline with legal enemy military intelligence.
`EnemyCompositionMemory` copies only the latest value-only Warrior, Archer, Lancer and
Monk snapshot produced by `FactionKnowledge`. Hidden units retain frozen counts, region
and last-seen time; confidence stays full briefly and then decays to zero. It contains no
enemy unit, object or transform reference, so hidden reinforcements are unknown until a
normal visible observation refreshes the snapshot.

Counter utility reads the authoritative `CombatDamage.Multiplier` table through a
read-only adapter. Observation confidence, sample size and weak remembered production
building evidence bound the reactive contribution. A maximum reactive share, combat
diversity floor, four-second strategic reevaluation, twelve-second desired-composition
hold, meaningful-change threshold and smoothing prevent single-unit overreaction and
strategy ping-pong. Stale intelligence gradually returns production to the baseline.
Enemy Monks increase bounded support threat and are not modeled as direct damage.

The policy compares desired counts with living plus FIFO-queued Red units. Monk demand
uses a capped support ratio. Candidate scoring includes composition deficit, counter
pressure, baseline need, affordability, producer availability, queue count and selection
inertia. Missing production infrastructure creates a hysteretic request for
`BuildingCommanderPolicy`; production falls back to a reasonable available unit instead
of spawning a building or stopping. One unpaid near-term unit cost feeds the Phase 7E.2
`EconomicDemandModel`; population and House decisions remain owned by the building and
population systems. All enqueue commands still pass through `FactionCommandService`.

### Phase 7E.4 tactical army intelligence

`TacticalArmyController` operates below the existing `MilitaryCommanderPolicy` strategic
state machine. It never starts a war, selects a macro objective, changes production or
replaces BaseDefense/Retreat. Its living combat snapshot contains only Warrior, Lancer,
Archer and Monk members; Pawn and the promoted Scout stay outside the army.

Warrior and Lancer are frontline, Archer is backline and Monk is support. Advance,
assembly and local regroup destinations reuse `RoleFormationPlanner` with an explicit AI
`Compact` formation independent of the human player's selected formation. The planner may
collapse naturally when stairs, corridors or occupancy cannot fit the ideal shape. In
active combat roles are preferences rather than a rigid line. A median position of living
non-support units supplies a robust army anchor, so one straggler or a rear Monk cannot
drag the center. Cohesion is a soft signal: serious fragmentation may regroup outside an
active fight, while ordinary spacing errors never interrupt combat or strategic retreat.

Local targeting considers only current `FactionKnowledge.VisibleEnemyUnits`. It scores a
bounded nearby candidate set using distance, the authoritative `CombatDamage.Multiplier`,
current visible health, active threat, role value, target stickiness and attacker
saturation. Enemy Monk receives high support priority and Archer receives bounded ranged
priority. A larger melee saturation penalty reduces whole-army crowding while allowing
stronger ranged focus. Assignments store enemy `EntityId` and copied observation values;
hidden live units or transforms are never retained. A hidden or invalid target can only be
selected again after normal vision makes it a current legal observation.

Warrior/Lancer committed melee attacks are not interrupted by routine reevaluation.
Archers move a short distance only when a visible melee threat enters a danger band based
on authoritative attack ranges; per-unit cooldown and destination dedupe prevent endless
kiting. Monks never attack. They use rear formation slots and make the same bounded local
escape when directly threatened. Monk strength is contextual: current health, nearby
combat ally coverage, nearby injury and authoritative heal throughput determine support
value. An isolated Monk contributes only a small fraction of its configured support value.

Tactical evaluation runs at a bounded 0.5-game-second cadence, reuses lists and maps, and
does not pathfind per target candidate. Formation plans update only for membership or a
meaningfully changed destination. Accepted Attack and Move commands still pass through
`FactionCommandService`; current targets, move destinations, hold time, cooldowns and the
existing CombatController state suppress duplicate commands. Optional diagnostics expose
state, anchor, cohesion, role counts, direct/support strength, per-unit targets and scores,
saturation, formation slots, commands, retargets and reposition counts.

### Phase 7E.5 strategic decision layer

`StrategicIntentEvaluator` is the single high-level rule-based decision layer. Every two
game seconds it copies a value-only `StrategicSituationSnapshot` from own faction state,
the existing economy/building/production/scouting/military policies, `FactionKnowledge`
and `StrategicSafetyMap`. It scores exactly six normalized intents: `DevelopEconomy`,
`Scout`, `Fortify`, `PrepareAttack`, `Attack` and `Recover`. An eight-second minimum hold,
score smoothing and a switch margin prevent routine strategy ping-pong. Immediate visible
base defense, existing strategic retreat/recovery and MatchEnd remain higher-priority
paths.

Enemy inputs are limited to copied `BuildingKnowledgeSnapshot` records and the decaying,
value-only `EnemyCompositionMemory`. Unknown military strength reduces readiness but is
never interpreted as confirmed zero. The evaluator stores only copied IDs, cells,
positions, scores and timestamps. It does not scan the scene, retain enemy components or
Transforms, move the Scout, allocate workers, enqueue units, build structures or issue
combat commands.

`AttackReadiness` combines living and queued strength, composition diversity, Monk
support, current army cohesion/state, legal intel confidence, target availability,
economic reserve, known target risk and recent losses. At most 24 remembered buildings
are cheaply scored per evaluation. Castle, military production, Tower and other completed
enemy buildings receive deterministic strategic values; age, visibility, distance, risk
and target continuity modify those values. Hidden building health and position remain
frozen until normal vision updates the knowledge snapshot.

`PrepareAttack` keeps the army in the existing rally/staging and role-formation pipeline.
Only `Attack` opens a new offensive operation, and the selected copied target is resolved
again against legal knowledge by `MilitaryCommanderPolicy`. Existing 7E.4 local combat,
formation, defender engagement, command dedupe and retreat logic remain authoritative.
An attack commitment window absorbs minor score changes. A failed operation records one
bounded launch marker and forces a recovery cooldown, preventing a newly trained single
unit from immediately starting another attack.

Economy, production, construction and scouting continue at their own established
cadences while any strategic intent is active. `Fortify` relies on current base defense
and the existing Tower capability; it introduces no new building type or Tower spam path.
`Scout` gives the existing scouting policy a bounded information-priority signal.
Legal enemy-building or military recon is considered first. While the enemy Castle
remains unknown, a safe frontier then takes priority over stale resource revisits;
this prevents resource memory from indefinitely delaying base discovery. Once
the Castle is known, stale re-observation retains its earlier priority over
ordinary frontier travel. Exhausted searches retry sooner. The scouting policy
still selects destinations and issues the command, and the one-active-Scout rule
is unchanged.
Optional Commander diagnostics expose current/previous intent, intent age, all six scores,
readiness, copied strengths/confidence, selected target and age, recent loss, staging cell,
reason and transition game time. The validator is available at
`Tools > Tiny Kingdom > Validate Phase 7E.5 Strategic Decision Layer`.

### Phase 7E.5 full-match liveness and spike diagnostics

Military command deduplication includes command health. An identical Move or chase is
retained only while its path is making progress or the unit has actually arrived. A
stopped or stale path can be requested again at a bounded retry interval; path failures
and last request/progress times remain available to disabled-by-default diagnostics.

Staging uses a majority fraction together with the robust army anchor and a bounded
straggler allowance. One delayed unit cannot hold the main force indefinitely. Rally
staging is valid only on explored, stable, unoccupied non-connector ground outside known
resources and friendly footprints. A no-progress watchdog invalidates stale formation
commands, requests an alternate staging cell after another stall, and then exits the
offense if the force still cannot assemble. New units and movement by one straggler do
not by themselves reset the whole army's stall clock. The strategic evaluator is asked
to reevaluate, and the recovery has a cooldown rather than issuing commands every frame.

Attack validates copied objectives against current legal knowledge. Re-observing an
empty remembered location completes that objective. A living offensive force with no
movement or combat progress performs a bounded command recovery and eventually
fails the objective rather than waiting forever. `Recover` retains its normal health,
strength, population and cohesion exit, and also leaves the withdrawal state after its
maximum duration so a depleted force can rebuild under normal production gates.

Tactical `Engaging` already leaves after its bounded no-enemy delay. Local regroup now
requires more than the tolerated straggler minority while advancing, preserving an
approximate formation without making one unit authoritative over the army.

The performance capture records bounded spike snapshots above a configurable frame-time
threshold (`-phase7BSpikeThresholdMs`). It keeps at most eight snapshots plus the peak,
including GC bytes, path and command bursts, strategic/tactical state, bounded candidate
sizes, audio pool size and the largest measured subsystem. Path counts distinguish Scout,
Economy/Builder and Military formation/combat/regroup sources. Use
`Tools > Tiny Kingdom > Profile Phase 7E.5 Liveness x10 (5 minutes)` for the long run and
`Tools > Tiny Kingdom > Validate Phase 7E.5 Strategic Liveness` for static configuration.
The profiler clears sparse marker samples between captures so a spike cannot inherit the
last duration from an earlier Commander tick. In the initial standalone baseline, the
largest intermittent spikes came from nested resource-slot path searches during new
worker assignments. The Commander now issues at most one new worker assignment per
think tick, and its initial Gather command leaves fallback selection to the economy
policy. A Pawn already farming still uses its normal depletion and retarget behavior.
An Economy Pawn in known civilian danger does not receive a new resource order while
trying to retreat. A valid retreat may cross dangerous cells to reach its safe exit;
the Commander replans if movement stops or the exit becomes dangerous. Failed
retreat searches and idle assignments wait the configured unreachable-resource retry
interval, and retry records are removed when roles change or Pawns die. Visible
resource assignments still run on the normal Commander tick. Hidden resource fallback
uses one rotating memory probe per retry interval: at most 128 remembered nodes are
scored and four approach cells are path-tested per probe. Resource type and approach
half rotate deterministically. Each resource type keeps its own memory cursor, so
repeated probes of that type cover all known resources.
A remembered resource that remains unseen after approach is temporarily blocked.

An active `Scout` strategic intent can otherwise wait indefinitely when every
Economy Pawn is continuously gathering. Promotion still prefers an idle Pawn. Once
Wood eligibility passes, after 60 game seconds without an idle candidate it
deterministically chooses the eligible busy Economy Pawn carrying the least cargo,
never a Builder or military unit. A pending building reservation can first require
its own 60 game-second wait, so both waits can apply. The authoritative promotion
command cancels that Pawn's old gather route only after the paid promotion succeeds.
If it carried resources, the new Scout first attempts a normal drop-off for up to
30 game seconds, then proceeds with exploration so an unreachable drop-off cannot
become a new scouting deadlock. Cargo remains carried if no drop-off can be reached;
it is not granted to the stockpile. After a successful deposit, the promoted Scout
does not automatically accept another gather job. Normal Fog and one-Scout limits
remain authoritative.

Profiling the first offense exposed a 27.7 ms pathfinding burst. Offensive
preparation defaults to one staging move per Commander tick in round-robin
order. A later trace showed that the first advance, still inside the preparation
marker, submitted four MilitaryCombat paths of about 5–6 ms each in one frame.
The advance now defaults to one AttackMove or Monk support move per Commander
tick and rotates through the force. Route failure is counted only after a complete
sweep of the members, so one rejected path cannot prematurely abandon the
objective. The visible-building assault defaults to the same one-command budget for
building attacks and Monk support moves. Immediate defense and retreat commands
keep their existing cadence; pathfinding rules and game speed are unchanged.

Near full exploration, the Scout rescout pass previously checked an approach ring
around every stale resource memory in one decision. A captured x10 frame spent
69.4 ms in that pass with about 330 known resources. The pass now evaluates a
rotating window of 16 memories per Commander tick by default (configurable up to 64),
covering the full memory list across subsequent evaluations without a single
large ring-search burst. A later five-minute x10 fixture still measured 19.8 ms
of Scout rescout scoring with the 32-memory default, so the default was reduced
to 16 while retaining the rotating coverage. Rejected Scout destinations share
the same per-tick window rather than starting three full windows. Long combat
profiling also traced repeated building perimeter path probes to attackers that
already had a progressing chase route. They now retain that route until it stops
or stalls; an obstructed unit still tests its existing step every frame while
redundant A* route searches back off to at most two game seconds until movement
resumes.

### Phase 7E.6 contested Gold Mine control

Mine extraction now gives each Pawn a 2.5x ordinary Gold Rock load after one
ordinary full-load work duration. The Pawn returns it to its own main Castle,
then automatically re-enters the mine. The AI still uses legal mine observations,
the worker safety gate and capacity; it receives no direct resource grant.

`SecureGoldMine` is evaluated alongside the existing strategic intents. The
`GoldMineStrategyModel` reads copied Gold Mine and Goblin observations from
`FactionKnowledge`; hidden objects do not refresh their last seen position,
health, owner, reserve or guard state. A bounded candidate pass considers reserve,
Gold need, travel distance, recent guard strength, army strength, available
population, economic workers and observed area. A Blue-owned mine remains a
candidate only when its observed value and Red's known military readiness
justify recapture. The army must contest it before a Red Pawn can enter;
`GoldMineObjective` applies the same unopposed local military-control rule to
both factions and transfers ownership only on successful Pawn entry. Visible
opposing Pawns ejected from the mine can be tactical targets during this
operation. An owned Red mine under a legally observed threat can
become a defense objective. Emergency base defense, retreat and recovery remain
above the mine score, and normal score smoothing/dwell time limits switching.
If known guards exceed the current army's strength, production temporarily raises
its military population target (bounded at 16) and returns to its ordinary
configured target when the mine opportunity expires. A ready army may approach a
recently observed mine that is currently hidden; it must regain vision before
Pawns are assigned.

The existing military policy executes a selected mine objective with its normal
staging, formation, AttackMove, tactical target selection, path retry and retreat
rules. Goblins are valid tactical targets and contribute to the existing combat
strength and safety evaluations. The mine is never attacked as a building during
the clearing operation. After clearing it, the army covers Pawn entry for up to
90 game seconds, then regroups if Red has claimed the mine or cools down a failed
attempt. No Gold is granted by the Commander. A remembered guard
loses certainty only with time and is removed when its last position becomes
visible without that Goblin; stale hidden units are not queried live.

Pawns enter a visible, sufficiently observed and safe mine through
`FactionCommandService.TryEnterGoldMine` and `GoldMineObjective.TryEnter` with
the existing civilian traversal policy. The worker policy reserves a bounded
number of eligible zero-cargo Pawns, leaves at least three Economy Pawns, and
uses the mine's authoritative capacity and extraction. Returning miners keep a
capacity reservation and the Mine role while carrying Gold to the Castle, so
ordinary Economy assignments cannot interrupt the return trip. Pending entries are released
when a known threat appears or the route remains unseen for 30 game seconds;
brief vision loss alone does not discard an already accepted entry order. A
threatened visible friendly mine ejects its occupants.
`TinyKingdom.AI.GoldMine.Score`, `.Threat` and `.AssignWorkers` are Profiler
markers; counters report candidates, worker assignments, failed entries and
ejections without per-frame logging. Structural checks are available at
`Tools > Tiny Kingdom > Validate Phase 7E.6 Contested Gold Mine Control`.
The authoritative local-control scan has its own
`TinyKingdom.GoldMine.Control` Profiler marker. Its periodic occupancy check
runs at most twice per game second, and entry commands validate control when
issued or completed. Only world-participating direct military on
the mine's elevation counts; internal Pawns remain protected by stationing.
The AI never infers control from hidden live Blue units. It can order a miner
into an observed opponent-owned mine only after its own military is locally
present and the ordinary visible-threat safety gate has passed.

Scout movement uses `UnitMovement.LastMovementProgressTime` to distinguish a
long valid trip from a stalled route. A stalled trip is stopped, its destination
is temporarily blocked, and target selection resumes. Three consecutive
batches of rejected destinations trigger an 18-game-second cooldown and clear
the `Repositioning` state; later scouting remains enabled. Counters expose
stalled-route recoveries, rejected destinations, last rejection reason and
failed-target recovery time without per-frame log strings.

Later RL work could observe legally known mine reserve, guard risk, army readiness
and current objective state, then choose a high-level contest or postpone action.
It would still use ordinary military, movement and mining authority.
