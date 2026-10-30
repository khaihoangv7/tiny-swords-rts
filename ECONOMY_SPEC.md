# Phase 5A economy rules

The MVP has Food, Wood, and Gold. Blue begins with 200 Food, 200 Wood, and 100
Gold; Red has a separate stockpile. Pawn costs 70 Food, carries 20 units, and
uses existing 8-direction navigation and separation. Tree, Sheep, and Gold
nodes use one generic `ResourceNode` and per-node interaction reservations.
Resource sprites are positioned using their imported runtime pivots. The Tree
base aligns with its logical node and click collider. A worker approaches the
resource-facing edge of its reserved neighboring cell and only begins work
after its movement path completes within 0.10 world units of that point.
Reachable cardinal slots are preferred; diagonal slots remain a fallback.
The interaction point is 0.49 cell widths from the reserved cell center
toward the node, still inside the reserved walkable cell.

Each Sheep contains 200 Food. Tree capacity is 125 Wood and base Gold Rock
capacity remains 100 Gold. Gathering timing, carry capacity and depletion behavior
were unchanged by that Sheep capacity adjustment; Phase 6H.1 below later raises
Pawn carry and per-impact collection independently.

One work cycle takes 1.5 seconds for Wood, Gold, and Food. The verified Axe,
Pickaxe, and Knife work visuals each play for 0.5 seconds, independently of
the gameplay cycle, then return to the matching Axe, Pickaxe, or Knife Idle
visual for a 1.0-second recovery. Gameplay applies up to two resource units at the configured 0.45-second
impact after each cycle begins. Work animation observes cycle events; sprite
frames and AnimationEvents never add resources.
Repeating a Gather order for the same resource while the job is active leaves
its timer, interaction reservation, and deposit trip intact. A Pawn also keeps
the 1.5-second impact interval when retargeted to another resource.

While traveling to a resource for an active gather job, the Pawn uses the
verified six-frame Run Axe, Run Pickaxe, or Run Knife visual for Wood, Gold,
or Food. This also represents travel to a legally remembered resource of the
same type. It is presentation only and does not change movement or work timing.

The Pawn uses verified Wood, Gold, or Meat carry Idle/Run frames while
transporting cargo, or after an interrupted job with retained cargo. During
Gathering recovery, the tool Idle visual has priority over cargo. A new command
cancels the worker job and its reservations but retains cargo. Explicit Move
uses generic Run when empty or the existing cargo Run when carrying. On node
depletion, the Pawn returns with cargo already gathered, or becomes Idle if empty.

Each Castle has three reserved physical drop-off positions near its front
entrance, at offsets (-0.8,-1.35), (0,-1.35), and (0.8,-1.35) world units.
The front navigation row is walkable; the interior remains occupied. A Pawn
deposits only after its path completes within 0.15 world units of its reserved
point. Depositing adds the carried amount to the stockpile, clears cargo,
releases the slot, and emits visual feedback above the Castle. A valid resource
target then starts another gathering trip.

A completed friendly Tower uses the same generic resource drop-off contract and
authoritative faction stockpile. Pawns sort valid Castle/Tower candidates by
distance and stable EntityId, then try reachable reserved interaction positions
in that order. Enemy, incomplete, destroyed and unreachable structures are
rejected. Losing the selected destination triggers a bounded recalculation while
the Pawn retains cargo.

Pawn combat follows `GAME_SPEC.md` and uses the normal `CombatController`,
`HealthComponent`, movement, and melee engagement slots. It requires an explicit
Attack command and uses Knife visuals. Worker jobs do not trigger automatic
enemy acquisition.

Phase 5C2.1 supports all four verified Tree variants. Tree1, Tree2, Tree3 and
Tree4 each loop their own eight-frame Tree tag at the source 10 FPS. Depletion
replaces only the visual with matching Stump1, Stump2, Stump3 or Stump4,
releases interaction reservations and logical occupancy, and leaves the node
depleted and ungatherable.

After depletion, invalidation or slot saturation, a Pawn searches the faction's
legally explored map for the nearest equivalent ResourceType. Currently visible
nodes must have real free reservation capacity. Hidden nodes are represented only
by frozen Fog-memory ID/type/position data; the Pawn travels to an explored
adjacent cell and reads live availability only after revealing the target again.
Unexplored nodes are excluded. Candidate ties use stable EntityId, and searches
run only on job transitions. Full cargo returns before continuing. Partial cargo
continues at another same-type node; if none is legally known, the Pawn deposits
that cargo and becomes Idle. Explicit Move, Attack, Gather, Build, Repair or
Garrison commands cancel the chain without discarding cargo.

House A/B/C use one authoritative 80 Wood `BuildingData`. Every completed House
owns at most one generated Sheep using the already verified Sheep animation and
200 Food capacity. The Sheep spawns on a bounded walkable ring outside the House
footprint. Depletion starts one 90-second gameplay-time coroutine; a living,
completed House then creates one replacement. House destruction cancels future
respawn while leaving an existing Sheep independent.

Sheep uses the verified Idle, Grass and Move tags at 10 FPS. Stationary Sheep
alternates Idle and Grass. Lightweight deterministic wandering stays within
1.75 world units on valid FlatGround or ElevatedGround at the original height. An interaction
reservation immediately stops and snaps the Sheep to its reserved logical cell
before the Pawn computes its approach, so harvesting never chases a moving
target. Sheep selection uses trigger-only coverage and Sheep never reserve hard
navigation occupancy. An otherwise legal building may be placed over Sheep;
successful placement cancels any active gather approach and moves affected Sheep
to distinct cells on the nearest bounded valid ring outside the footprint. The
existing harvest flash, Food impact and depletion rules remain authoritative.

Gold Stones 1 through 6 contain 100, 150, 250, 300, 400 and 500 Gold. Each
successful mining impact plays that Stone's six-frame Highlight tag, including
its verified 0.5-second final hold, then restores the matching Still sprite.
Highlight playback does not change the 1.5-second gather cycle or resource
amount.

Every non-depleted Gold Stone also runs an independent ambient Highlight cycle.
After a Highlight finishes, its 10-second configurable MVP wait begins; the
next Highlight starts only after that wait. A mining impact during the wait
starts Highlight immediately. An impact during active playback does not stack
or restart frames, avoiding flicker. Completion always resets the full
10-second wait. Depletion stops playback and its timer permanently.

## Phase 7B Red economy integration

Red production and construction consume the same `ResourceStockpile` used by
Red Pawn deposits. The Commander never adds or removes resources directly.
Building payment occurs once through `BuildingSystem.TryBuild`; unit payment and
population reservation occur once when `UnitProductionQueue.TryTrain` accepts a
request.

Red grows toward six living-or-queued Pawns. Newly produced Pawns enter the
existing 40/35/25 Food/Wood/Gold assignment policy. Economy assignments exclude
the active Scout and Builder roles. Construction preserves at least two Economy
Pawns when possible and non-emergency infrastructure waits while the Red Food
stockpile is below the 70 Food cost of one Pawn.

Population pressure uses `Cap - Used - Reserved`. At two or fewer free slots the
Commander prioritizes one House project and waits for its normal completion
before considering another. No resource, population or construction shortcut is
available to AI.

## Phase 7D long-match economy baseline

The 40/35/25 worker distribution remains a target rather than a hard requirement.
When the preferred resource type has no safe known target, the worker tries another
type by current deficit and stable enum order. When no safe legal work exists it may
remain Idle near home; it must not cross known danger to preserve a ratio.

Builder completion/death/interruption and Scout release return a Pawn to the shared
Economy role through `CommanderPawnRoles`. One under-construction House satisfies
the current capacity plan until completion; population uses `Cap - Used - Reserved`
exactly once. Destroyed core Barracks, Archery or Monastery becomes absent from
authoritative own-building knowledge and may be planned again when resources,
worker reserve and placement permit. Phase 7D grants no free income, construction,
population or production.

## Phase 7E.2 adaptive economy

Red worker allocation is demand driven. The Commander derives Food, Wood and Gold
deficits from current stockpiles, safety buffers and only unpaid near-term plans.
Resources already spent on an accepted building or production queue entry are not
counted again. Reallocation is stable: planning runs every four game seconds, changes
at most one empty-handed Pawn, and observes an eight-second role dwell outside a
critical shortage.

Resource selection remains knowledge legal. Visible nodes may use current amount and
capacity; hidden nodes use frozen last-seen ID, type, position and amount. Worker travel,
nearest completed friendly Castle/Tower, crowding, depletion and known danger determine
the target score. A completed economic Tower therefore shortens legal deposit routes
without changing gather, carry, payment or depletion authority.

The outpost planner groups a bounded set of known value snapshots. It may construct one
safe Tower near a rich distant cluster only when the authoritative 125 Wood / 85 Gold
cost is affordable beyond current buffers and reservations. Existing nearby drop-offs,
danger, small clusters, active projects and cooldown reject the plan. House planning
continues to use population pressure and the authoritative 80 Wood cost; Sheep spawning
does not create House demand.

## Phase 7E.6 Gold Mine exploitation

The contested Gold Mine remains a station, not a `ResourceNode`: the first Pawn
to enter claims it, and at most three Pawns work inside it at once. Each Pawn
works for an ordinary full Gold Rock load duration (currently ten 1.5-second
cycles, or 15 seconds), then exits with up to 2.5 ordinary loads of Gold
(currently 50). The final load is capped by the remaining 2,000-Gold reserve.
The Pawn carries this Gold through the existing cargo/return movement system
and deposits it only at its own main Castle; the mine does not add Gold to the
stockpile directly and a Tower cannot accept this mine cargo. After delivery,
the Pawn automatically returns for another load if the mine is usable. A
returning Pawn keeps its mine capacity reservation until re-entry or job
cancellation. Other resource cargo still uses the normal Castle/Tower rule.
Red's worker policy uses the visible mine's
authoritative entry command only after legal knowledge and the civilian safety
check consider the area clear. It keeps at least three Pawns in ordinary Economy
roles and prefers idle or Gold workers with no cargo. Rejected entry attempts
have a retry interval; pending orders are released if danger returns. Depletion,
destruction, ownership, ejection and extraction remain owned by `GoldMineObjective`.
For recapture, an unopposed opposing Warrior, Lancer or Archer within seven
world units ejects current miners. A Pawn of that faction can enter only while
that uncontested military presence remains; successful entry transfers the
mine's persistent owner and resets the payout interval. This applies to both
playable factions. The AI still waits for a visible, sufficiently safe approach
and leaves ordinary Food and Wood workers available.
An already accepted Pawn entry order survives brief loss of mine vision while
traveling, but is canceled on legally observed danger or after 30 game seconds
without a fresh safe observation. The army covers a newly cleared mine long
enough for Pawns to occupy it, with a 90-game-second limit.

Phase 7E.7 keeps the same extraction, cargo amount, Castle-only deposit, reserve
and ownership rules. When one mine entrance is blocked, the same authoritative
entry action probes at most four perimeter slots at once and remembers at most
twelve attempted slots while recovering a stalled approach. A Pawn claims only
after reaching its reserved standing cell. Pending miners retain their assigned
role until claim or cancellation.
Banked Gold alone is spendable. Carried Gold and a conservative next-load
forecast only inform ordinary Gold-worker allocation; a hidden or depleted
mine contributes no expected load. A canceled repeat trip still delivers any
Gold already in cargo to the main Castle. Dead Pawns lose their carried cargo,
consistent with the existing worker death behavior; no new resource drop is
created.
