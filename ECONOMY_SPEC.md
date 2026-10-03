# Phase 5A economy rules

The MVP has Food, Wood, and Gold. Blue begins with 200 Food, 200 Wood, and 100
Gold; Red has a separate stockpile. Pawn costs 50 Food, carries 20 units, and
uses existing 8-direction navigation and separation. Tree, Sheep, and Gold
nodes use one generic `ResourceNode` and per-node interaction reservations.
Resource sprites are positioned using their imported runtime pivots. The Tree
base aligns with its logical node and click collider. A worker approaches the
resource-facing edge of its reserved neighboring cell and only begins work
after its movement path completes within 0.10 world units of that point.
Reachable cardinal slots are preferred; diagonal slots remain a fallback.
The interaction point is 0.49 cell widths from the reserved cell center
toward the node, still inside the reserved walkable cell.

Each Sheep contains 200 Food. Tree 100 Wood and base Gold Rock 100 Gold remain
provisional capacities. Gathering timing, carry capacity and depletion behavior
were unchanged by that Sheep capacity adjustment; Phase 6H.1 below later raises
Pawn carry and per-impact collection independently.

One work cycle takes 1.5 seconds for Wood, Gold, and Food. The verified Axe,
Pickaxe, and Knife work visuals each play for 0.5 seconds, independently of
the gameplay cycle, then return to the matching Axe, Pickaxe, or Knife Idle
visual for a 1.0-second recovery. Gameplay applies up to two resource units at the configured 0.45-second
impact after each cycle begins. Work animation observes cycle events; sprite
frames and AnimationEvents never add resources.

The Pawn uses verified Wood, Gold, or Meat carry Idle/Run frames while
transporting cargo, or after an interrupted job with retained cargo. During
Gathering recovery, the tool Idle visual has priority over cargo. A new command
cancels the worker job and its reservations but retains cargo. On node depletion, the Pawn returns
with cargo already gathered, or becomes Idle if empty.

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
stockpile is below the 50 Food cost of one Pawn.

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
