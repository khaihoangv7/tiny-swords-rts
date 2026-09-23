# Phase 5A economy rules

The MVP has Food, Wood, and Gold. Blue begins with 200 Food, 200 Wood, and 100
Gold; Red has a separate stockpile. Pawn costs 50 Food, carries 10 units, and
uses existing 8-direction navigation and separation. Tree, Sheep, and Gold
nodes use one generic `ResourceNode` and per-node interaction reservations.
Resource sprites are positioned using their imported runtime pivots. The Tree
base aligns with its logical node and click collider. A worker approaches the
resource-facing edge of its reserved neighboring cell and only begins work
after its movement path completes within 0.10 world units of that point.
Reachable cardinal slots are preferred; diagonal slots remain a fallback.
The interaction point is 0.49 cell widths from the reserved cell center
toward the node, still inside the reserved walkable cell.

The provisional capacities are Tree 100 Wood, Sheep 60 Food, and Gold Rock 100
Gold. These and the work rate require visual balance review before Phase 5B.

One work cycle takes 1.5 seconds for Wood, Gold, and Food. The verified Axe,
Pickaxe, and Knife work visuals each play for 0.5 seconds, independently of
the gameplay cycle, then return to the matching Axe, Pickaxe, or Knife Idle
visual for a 1.0-second recovery. Gameplay applies one resource unit at the configured 0.45-second
impact after each cycle begins. Work animation observes cycle events; sprite
frames and AnimationEvents never add resources.

The Pawn uses verified Wood, Gold, or Meat carry Idle/Run frames while
transporting cargo, or after an interrupted job with retained cargo. During
Gathering recovery, the tool Idle visual has priority over cargo. A new command
cancels the worker job and its reservations but retains cargo. On node depletion, the Pawn returns
with cargo already gathered, or becomes Idle if empty.

The Blue Castle has three reserved physical drop-off positions near its front
entrance, at offsets (-0.8,-1.35), (0,-1.35), and (0.8,-1.35) world units.
The front navigation row is walkable; the interior remains occupied. A Pawn
deposits only after its path completes within 0.15 world units of its reserved
point. Depositing adds the carried amount to the stockpile, clears cargo,
releases the slot, and emits visual feedback above the Castle. A valid resource
target then starts another gathering trip.

Pawn combat follows `GAME_SPEC.md` and uses the normal `CombatController`,
`HealthComponent`, movement, and melee engagement slots. It requires an explicit
Attack command and uses Knife visuals. Worker jobs do not trigger automatic
enemy acquisition.

Phase 5C2.1 supports all four verified Tree variants. Tree1, Tree2, Tree3 and
Tree4 each loop their own eight-frame Tree tag at the source 10 FPS. Depletion
replaces only the visual with matching Stump1, Stump2, Stump3 or Stump4,
releases interaction reservations and logical occupancy, and leaves the node
depleted and ungatherable.

After depletion or invalidation, a Pawn searches within the configurable MVP
`AutoResourceSearchRadius` of 6 for the nearest reachable non-depleted node of
the same ResourceType. Candidate ordering is stable by distance, position and
name; each route and interaction reservation must succeed. This search happens
on job transitions, not every frame. Full cargo returns before continuing.
Partial cargo continues at a nearby same-type node; if none is available in
range, the Pawn deposits that cargo and becomes Idle. Explicit Move, Attack,
Gather, Build, Repair or Garrison commands cancel the chain without discarding
cargo.

Sheep uses the verified Idle, Grass and Move tags at 10 FPS. Stationary Sheep
alternates Idle and Grass. Lightweight deterministic wandering stays within
1.75 world units on valid FlatGround at the original height. An interaction
reservation immediately stops and snaps the Sheep to its reserved logical cell
before the Pawn computes its approach, so harvesting never chases a moving
target. The existing harvest flash, Food impact and depletion rules remain
authoritative.

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
