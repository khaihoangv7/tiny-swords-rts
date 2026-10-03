# Tiny Kingdom RL — Game Specification

## 1. Project Overview

Tiny Kingdom RL is a simplified real-time strategy game inspired by Age of Empires.

The goal is to create a small RTS that is easy to understand, easy to balance, and suitable for later Reinforcement Learning experiments.

Engine:

Unity

Language:

C#

Main art pack:

Tiny Swords by Pixel Frog.

The game must first work completely without Reinforcement Learning.

---

# 2. Core Gameplay

There are two factions:

* Blue
* Red

Each faction controls:

* one Castle
* Pawns
* military units
* buildings
* resources

The game runs in real time.

The player gathers resources, builds structures, trains units, and attacks the enemy.

---

# 3. Victory and Defeat

Each faction owns one main Castle.

Victory condition:

Destroy the enemy Castle.

Defeat condition:

Your Castle is destroyed.

The game immediately ends when one Castle reaches 0 HP.

There are no alternative victory conditions in the first version.

---

# 4. Starting State

Each faction starts with:

* 1 Castle
* 3 Pawns

Starting resources:

Food = 200

Wood = 200

Gold = 100

Starting population limit:

10

Both factions should start with approximately equal access to resources.

---

# 5. Resources

The game contains three resources:

## Food

Main source:

Sheep.

Each Sheep contains 200 Food.

Sheep are soft world entities. Their interaction colliders remain selectable
triggers, but Sheep do not reserve logical navigation cells, block A*, take part
in unit separation, or physically block units. Ambient wandering still avoids
choosing a cell occupied by a living unit where practical.

Used for:

* Pawns
* Warriors
* Lancers

---

## Wood

Main source:

Trees.

Used for:

* Buildings
* Archers
* Houses

---

## Gold

Main source:

Gold Rocks.

Used for:

* Warriors
* Lancers
* advanced structures if added later

Warrior requires a significant amount of Gold because it is the strongest and most expensive military unit in the initial game.

---

# 6. Pawn

Pawn is the worker unit.

Pawn can:

* move
* gather wood
* gather food
* gather gold
* return resources
* construct buildings
* repair buildings

Pawn is primarily a worker and may fight only after an explicit Attack command.
Pawn does not automatically acquire enemy targets while working or Idle. Enemy
military units may target, damage, and kill Pawns through the normal combat system.

Phase 5C1 Pawn combat data: 50 HP, 2 base melee damage, 1 world-unit attack
range, 1.4-second attack interval, 3 world-units/second movement, and
1 population. These are MVP values; the existing 50 Food cost remains.

Cost:

50 Food.

Carry capacity:

20 resource units.

---

# 7. Resource Gathering

The player selects a Pawn and gives it a resource target.

Example:

Pawn + Tree

becomes:

Gather Wood.

Pawn + Gold Rock

becomes:

Gather Gold.

Pawn + Sheep

becomes:

Gather Food.

The gather loop is:

Each Pawn's successful resource gather uses a 1.5-second cycle for Wood, Gold,
and Food. Work animation playback is independent of that gameplay timer;
resource gain occurs at a configured gameplay impact time, followed by a
recovery delay until the next cycle. Each successful impact collects up to 2
units without exceeding the resource remainder or Pawn carry capacity.

Gold gathering uses a reserved adjacent navigation cell. Gold Stone 1-5 use a
small inward interaction offset so the Pawn remains visibly close enough to the
rock. The larger Gold Stone 6 uses an outward offset so the Pawn remains outside
its artwork before the mining cycle begins.

Move to resource

→ Gather

→ Inventory reaches carry capacity

→ Return to nearest valid friendly Castle or completed Tower

→ Deposit resources

→ Return to resource

→ Continue gathering.

If the resource is depleted, the Pawn becomes Idle.

An explicit Move or Attack order cancels the current worker job and releases its
resource/Castle interaction reservation. Any already gathered cargo stays on
the Pawn. A later Gather order may resume work, depositing mismatched cargo
before gathering a different resource.

---

# 8. Unit Types

All gameplay unit artwork uses a presentation-only 1.6 scale on its animated
visual child. The unit gameplay root remains `(1,1,1)`, and movement, collider,
separation, attack range, projectile origin, selection logic and navigation do
not derive from this visual scale. Animation frame offsets are scaled around
their verified animation anchor so the unit's ground contact remains stable.
Unit health bars remain unscaled and use a raised presentation anchor.
Normal Tree and Sheep visual children use the same 1.6 world-asset scale.
Gold Stone 1-5 use 1.4 and Gold Stone 6 uses 1.55 so the largest verified node
has stronger visual weight. All retain their existing gameplay roots, colliders,
occupancy and interaction slots. Terrain tiles, UI, cursors, projectiles and VFX
retain their own established presentation scales. Buildings retain their
separate profiles.

The initial game contains:

* Pawn
* Warrior
* Archer
* Lancer

No additional military units are required for MVP.

Military units are intentionally asymmetric.

Warrior is the strongest individual military unit.

Archer provides ranged damage.

Lancer is a cheaper anti-Warrior unit designed to be effective when used in groups.

---

# 9. Warrior

Role:

Elite melee unit.

Warrior is the strongest and most expensive individual military unit in the initial game.

Warrior should be powerful enough to defeat any single Lancer in a direct 1v1 fight under normal conditions.

Cost:

100 Food

80 Gold

Initial stats:

HP = 260

Damage = 28

Attack Range = 1

Attack Interval = 1.1 seconds

Move Speed = 3

Warrior is intended to counter Archer.

Warrior characteristics:

* very strong melee combat
* high HP
* high damage
* expensive
* requires significant economy
* vulnerable to groups of Lancers
* should not be easily replaceable

One Warrior should normally defeat one Lancer.

Two Lancers should normally defeat one Warrior.

This relationship is an intentional balance rule.

## Warrior Guard

Only a living Warrior can Guard. Holding G (or the Guard command button) enters
Guarding; releasing that input returns a Guarding Warrior to Idle. Guard is not a
toggle. The Warrior remains selectable.

Entering Guard cancels its current movement and combat target. While Guarding it
cannot move, chase, or attack. A new Move or Attack command immediately exits
Guard and then runs normally. Continuing to hold the original input does not
reactivate Guard; a new press is required.

Guard reduces incoming non-projectile damage to 0.75 and incoming projectile
damage to 0.50 after the existing counter multiplier. Guard status for a
projectile is determined when it hits, not when it is fired. Calculate positive
damage as BaseDamage × CounterMultiplier × GuardMultiplier and round once with
0.5 upward. Actual HP loss is capped by remaining HP. Without Guard, its
multiplier is 1.00. Existing counters, stats, and the normal Warrior/Lancer
matchup rules remain unchanged.

---

# 10. Archer

Role:

Ranged damage unit.

Cost:

40 Food

40 Wood

Initial stats:

HP = 60

Damage = 10

Attack Range = 5

Attack Interval = 1.2 seconds

Move Speed = 3

Archer attacks using projectiles.

An Archer attack cycle begins its Shoot presentation immediately. Its gameplay
release delay is configured as 0.7 seconds; the projectile is created when
that delay elapses if the attack remains valid. The 1.2-second Attack Interval is
measured from the start of each cycle, including the release delay. Cancelling
the attack before release cancels the pending projectile. A released projectile
still evaluates Guard and damage when it hits.

Projectile visuals must not determine damage timing.

CombatSystem determines actual damage.

Archer is intended to counter Lancer.

Archer characteristics:

* ranged attack
* low HP
* vulnerable in melee
* relatively cheap
* strong when protected by other units

---

# 11. Lancer

Role:

Anti-Warrior melee unit.

Lancer is cheaper and individually weaker than Warrior.

Its purpose is to provide a cost-effective response to Warriors when multiple Lancers are used together.

Cost:

60 Food

40 Gold

Initial stats:

HP = 125

Damage = 18

Attack Range = 1.5

Attack Interval = 1.0 seconds

Move Speed = 2.8

Lancer receives bonus damage against Warrior through the counter system.

Important balance rule:

1 Lancer should normally lose against 1 Warrior.

2 Lancers should normally defeat 1 Warrior.

Therefore:

Lancer counters Warrior through numbers and cost efficiency.

Lancer does NOT counter Warrior by automatically winning a 1v1 fight.

---

# 12. Counter System

The initial counter relationship is:

Warrior > Archer

Archer > Lancer

Lancer > Warrior

However, counters are SOFT COUNTERS.

A counter does not automatically guarantee victory in a 1v1 fight.

Unit base strength, cost, HP, damage, attack speed, positioning, and number of units still matter.

---

## Warrior vs Archer

Warrior receives an advantage against Archer.

Warrior → Archer:

Damage multiplier = 1.25

Archer → Warrior:

Damage multiplier = 0.80

Warrior should strongly defeat Archer if it successfully reaches melee range.

---

## Archer vs Lancer

Archer receives an advantage against Lancer.

Archer → Lancer:

Damage multiplier = 1.25

Lancer → Archer:

Damage multiplier = 0.80

Archer should perform well against Lancers when it has enough distance to attack.

---

## Lancer vs Warrior

Lancer receives a stronger counter bonus against Warrior.

Lancer → Warrior:

Damage multiplier = 1.50

Warrior → Lancer:

Damage multiplier = 1.00

This is intentionally asymmetric.

Even with the counter bonus:

One Lancer should NOT normally defeat one Warrior.

The expected relationship is approximately:

1 Warrior > 1 Lancer

1 Warrior < 2 Lancers

This rule is more important than the exact numerical stats.

If balance testing shows that one Lancer can consistently defeat one Warrior, adjust unit stats or multipliers.

If two Lancers consistently fail to defeat one Warrior, adjust unit stats or multipliers.

Do NOT change this intended relationship without updating GAME_SPEC.md.

---

# 13. Combat Balance Philosophy

Military units are not intended to have equal individual strength.

Approximate individual power hierarchy:

Warrior > Lancer > Archer

Approximate cost hierarchy:

Warrior > Lancer > Archer

However, tactical counters change resource efficiency.

Example:

A Warrior is stronger than one Lancer.

But producing multiple Lancers can be an effective economic response to an enemy investing heavily in Warriors.

This creates strategic choices:

Expensive elite army

versus

larger specialized counter army.

The Reinforcement Learning agent should eventually be able to discover these relationships through gameplay.

---

# 14. Population

Every unit consumes population.

Pawn:

1 population.

Warrior:

1 population.

Archer:

1 population.

Lancer:

1 population.

Starting population cap:

10.

House increases population cap by:

+5.

Units cannot be trained if the population cap would be exceeded.

---

# 15. Buildings

Normal faction building art is rendered at presentation scale 1.7 on a
ground-anchored visual child. The gameplay root remains scale 1.0. Logical
footprints, construction occupancy, A* blockers, interaction slots, rally,
drop-off and combat authority do not derive from sprite bounds. Elevation-aware
sorting projects the building's logical elevation into its ground contact using
`projectedY = groundY - elevation * cellSize`, then uses that value in the same
global depth domain as units, resources and terrain foreground faces. This allows
a lower foreground unit to render over a higher object behind it and a unit north
of a building's ground line to pass behind upper roof artwork. Health/construction bars,
damage effects and mounted Archer anchors are positioned from the scaled visual
but do not inherit its transform scale. Gold Mine keeps its separately tuned
2.60 presentation scale.

The Main Castle retains its authoritative 3x2 gameplay footprint and collider.
Its wider 1.7 presentation silhouette reserves one additional navigation cell
on each horizontal side so routes cannot clip through the visible stone edges.
The front approach remains open for Castle drop-off and garrison entry.

`Building Visual Bounds != Building Physical Footprint.` `BuildingRoot` stays
at `(1,1,1)`; only its `VisualRoot` may use presentation scale.

Phase 6F.1 applies unified projected ground-depth sorting to units, Sheep, Trees,
Gold, buildings, decorative world props, southern cliff faces and official stairs.
Unit feet and resource/building ground contacts are sort anchors. Full terrain
Tilemaps stay background-only and in Chunk mode. This is presentation only:
navigation, occupancy, combat, economy, Fog authority and balance values are unchanged.

Every successful positive Wood harvest may trigger one subtle Tree visual
wobble (3.5 degrees for 0.22 seconds). The resource root and interaction area
remain fixed, repeated impacts refresh the current wobble, and chopping audio
remains independently driven by the authoritative gather impact.

Clicking a selectable friendly normal building plays one presentation-only
vertical pulse: 0.94 squash, 1.035 stretch, then recovery to its configured
visual scale. This is intentionally gentler than the 0.84/1.12 unit damage
reaction and does not change the building root, footprint, collider or state.

The initial game contains:

* Castle
* House
* Barracks
* Tower
* Archery
* Monastery

House is a population building and never trains units. Castle trains Pawn,
Barracks trains Warrior and Lancer, Archery trains Archer, and Monastery trains
Monk. These specialized rosters replace the previous generic Barracks roster.

---

# 16. Castle

The Castle is the main faction building.

Functions:

* determines win / lose condition
* acts as resource drop-off point
* represents the faction headquarters

If Castle HP reaches 0:

that faction loses immediately.

Phase 5C2 defensive data:

* MaxHP = 2000 from Phase 6C.2
* Internal Garrison capacity = 5 living friendly Pawn/Warrior/Archer/Lancer
* Mounted Archer capacity = 3
* each internal occupant contributes one generic 5-damage projectile per volley
* an empty Castle fires no garrison arrows

The main Castle has VisionRadius 9. When a Pawn physically reaches the main
Castle and successfully enters its internal garrison, any carried resource is
deposited through the shared drop-off transaction before the Pawn is hidden.
Cancelling the approach does not deposit cargo. A completed friendly Tower is
also a normal external Food/Wood/Gold drop-off; this does not require garrison
entry and never makes an enemy or incomplete Tower valid.

---

# 17. House

Cost:

80 Wood.

Function:

Increase population capacity by 5.

The player may build multiple Houses. House1, House2 and House3 are visual
variants of the same House gameplay type; each has the same cost, HP, logical
footprint, construction behavior and +5 population bonus.

Each completed House owns at most one generated Sheep. The initial Sheep spawns
on a nearby valid walkable cell outside the House footprint. If that Sheep is
depleted, the still-living completed House creates one replacement after 90
seconds of gameplay time. Destroying the House cancels future replacement but
does not destroy an already living Sheep.

---

# 18. Barracks

Cost:

150 Wood.

Function:

Train Warrior and Lancer.

Available units:

* Warrior
* Lancer

Each training action requires resources and time.

Only one unit should be produced per training request.

---

# 19. Tower

Cost:

75 Wood

50 Gold.

Function:

Fires defensive arrows only when friendly units are stationed inside or when
mounted Archers are present. An empty Tower has no free attack.

Tower target priority:

nearest valid enemy unit.

Tower cannot move.

Phase 5C1 Tower MaxHP = 300. Tower garrison and attack changes are deferred to
Phase 5C2.

Phase 5C2 Tower rules:

* Internal Garrison capacity = 2 living friendly Pawn/Warrior/Archer/Lancer
* Mounted Archer capacity = 1
* each internal occupant contributes one generic 5-damage projectile per volley
* mounted Archers are separate and do not add generic garrison arrows

---

# 19A. Archery and Monastery

Archery trains Archer after construction. Its Phase 5C1 test values are
provisional: 150 Wood, 250 HP, 3x3 logical cells, 7-second construction.

Monastery trains Monk. Its Phase 5C1 building values remain provisional:
150 Wood, 50 Gold, 300 HP, 3x3 logical cells, 8-second construction.

---

# 19B. Unit Production MVP

Training uses a FIFO queue of at most five requests per building. Each request
spends its unit cost and reserves its population at enqueue time. The queue
rejects requests without resources or available population. One request trains
at a time; production pauses while its building is incomplete. A finished
request waits if every nearby spawn cell is blocked or the population cap has
since fallen. Destroying a producer discards its queue and releases reserved
population; resources already spent are not refunded in this MVP.

Provisional gameplay training times: Pawn 4 seconds, Warrior 8 seconds,
Archer 6 seconds, Lancer 6 seconds and Monk 7 seconds. Animation frames do not
control training.

VisionRadius is stored as configurable unit/building data for later vision
work. Phase 5C1 does not calculate visibility or render Fog of War; current
test values are provisional.

---

# 19C. Tower and Castle Defense MVP

Internal occupants are hidden and suspended from movement, selection, normal
combat, collision, separation and direct targeting. Their HP, faction, unit
type and Pawn cargo remain unchanged. Mounted Archers remain visible at explicit
roof anchors, cannot move or be targeted while the host survives, and retain
their existing Shoot windup, projectile release, attack interval, counter rules
and Guard-at-impact behavior.

Mounted Archer final damage is the normal counter-adjusted Archer hit multiplied
by 1.20, rounded using the existing positive half-up rule. Mounted attack range
is base Archer range multiplied by the provisional 1.25 range multiplier. The
normal UnitCombatData asset is never rewritten.

Generic defensive arrows use projectile delivery, 5 base damage and no unit
counter multiplier. Projectile Guard mitigation still applies at impact. The
number of arrows in one volley equals the current internal occupant count.
Targets are sorted by distance, then world position and stable entity identity;
arrows are distributed round-robin, so multiple arrows may hit one target when
there are fewer targets than arrows.

The following defense values are provisional MVP balance values:

* defensive volley interval: 1.5 seconds for Tower and Castle
* Tower defensive attack range: 7 world units
* Castle defensive attack range: 8 world units
* mounted range multiplier: 1.25
* Castle MaxHP: 2000 (raised by Phase 6C.2)

Tower and Castle use explicit projectile-origin and rooftop-anchor offsets from
BuildingData. Right-clicking a friendly completed defensive building orders
selected units by nearest distance. Archers reserve rooftop slots first and
then internal slots; other supported units reserve internal slots. Units walk
to distinct cardinal interaction positions before entering.

On voluntary Eject All or host destruction, occupants are restored at distinct,
nearby, walkable and unoccupied grid positions. Host occupancy is released
before destruction ejection. Occupants keep their HP and cargo and receive no
collapse damage. No verified ruin sprites are currently mapped, so the destroyed
host visual is hidden.

Living world units and defensive buildings register queryable VisionRadius
sources. Tower and Castle vision radius remains independent from attack range.
A future Fog-of-War system will darken areas outside current faction vision and
query these sources. Phase 5C2 does not render darkness, retain explored memory,
hide enemies or implement minimap visibility.

---

# 20. Building Construction

Pawns construct buildings.

Construction sequence:

Player selects Pawn

→ Player selects building type

→ Player selects valid location

→ Pawn moves to location

→ Construction begins

→ Building HP increases until complete

→ Building becomes active.

Buildings cannot overlap:

* water
* cliffs
* resources
* another building

Building placement must respect the logical map grid.

---

# 21. Building Repair

A damaged friendly building can be repaired by a Pawn.

Repair sequence:

Pawn receives Repair order

→ moves into repair range

→ repairs over time

→ stops when building reaches maximum HP.

Repair uses the same verified Hammer animation as construction. Its gameplay
cycle is 0.8 seconds with an impact at 0.35 seconds. At each successful impact,
HP is restored from the configured `RepairHPPerSecond` rate and the building
plays the same mild ground-anchored vertical pulse used by building selection.
Animation frames do not authorize HP restoration.

Repair must consume resources only if explicitly implemented later.

For MVP:

Repair does not consume resources.

---

# 22. Unit Selection

Player controls units using mouse input.

Left Click:

Select one unit.

Shift + Left Click:

Add / remove unit from selection.

Left Mouse Drag:

Box select multiple units.

Click empty ground:

Clear selection unless dragging.

---

# 23. Unit Orders

Military units support:

Move

Attack

AttackMove

Stop

Pawns additionally support:

Gather

Explicit Attack

Build

Repair

ReturnResource

---

# 24. Move Order

Right-clicking walkable ground gives selected units a Move order.

Units pathfind toward the destination.

Units stop when reaching an acceptable destination radius.

Rapid Move commands that resolve to the same logical destination cell and are
within a 0.2-world-unit command epsilon keep the active route. A replacement
route omits the A* start-cell center, because that point may already be behind
a unit moving between cell centers. The newest genuinely different destination
still replaces the route normally.

Living military units maintain a small configurable personal space when
moving or standing near each other. Local separation does not reserve logical
grid cells or change their terrain/building occupancy. It cannot move a unit
through Water, Cliff, or an invalid height transition.

---

# 25. Attack Order

Right-clicking an enemy unit or enemy building gives an Attack order.

Sequence:

Move into attack range

→ Attack

→ Continue attacking until target dies

→ Return to Idle.

When possible, multiple melee attackers on the same target reserve distinct
valid engagement directions around it. The reserved direction guides combat
positioning and does not change the normal attack-range check. Engagement
slots handle preferred combat positions; local UnitSeparation handles nearby
unit spacing and incidental crowding.

---

# 26. Attack Move

AttackMove means:

Move toward destination.

If an enemy is detected:

temporarily interrupt movement

→ chase enemy

→ attack

→ when enemy dies, continue toward the original destination.

The player presses `A`, then left-clicks valid ground to issue AttackMove to
selected Warrior, Archer and Lancer units. Pawn is excluded. A successful
command exits the mode; an invalid left-click destination keeps it armed so the
player can try again. Escape cancels the pending mode. Right Click while armed
cancels AttackMove targeting and then performs the normal contextual Right
Click command for that same click.

---

# 27. Autonomous Combat

Military units may automatically respond to nearby enemies.

A unit should only auto-acquire targets when it is:

Idle

or performing AttackMove.

A simple Move command should not automatically become an aggressive chase unless explicitly designed later.

---

# 28. Unit State Machine

Military units use gameplay states such as:

Idle

Moving

Chasing

Attacking

Dead.

Pawns may additionally use:

Gathering

ReturningResource

Building

Repairing.

Gameplay state and animation state must remain separate.

Gameplay determines what is happening.

Animation only displays it.

---

# 29. Damage

Basic damage calculation:

FinalDamage =
BaseDamage × CounterMultiplier.

Attack Interval determines how often a unit can deal damage.

Damage Per Second must NOT replace actual attack timing in gameplay.

Each attack occurs according to the unit's Attack Interval.

HP may not go below 0.

At HP <= 0:

the entity dies.

Dead units cannot:

* move
* attack
* gather
* receive new orders.

---

# 30. Combat Timing

Combat must be based on gameplay timers.

Animation frames must not be the authoritative combat timer.

Example:

Warrior Attack Interval = 1.1 seconds.

This means the Warrior cannot apply damage more than once every 1.1 seconds.

Animation should visually match this interval as closely as practical.

However:

CombatSystem determines when damage occurs.

Animation does not determine whether an attack is valid.

This is important for deterministic gameplay and future Reinforcement Learning training.

---

# 31. Unit Death

When a unit dies:

1. gameplay immediately marks the unit as dead
2. it stops interacting with game systems
3. death animation may play
4. after the animation or configured delay, the GameObject may be removed

Death animation must not determine whether the gameplay unit is alive.

---

# 32. Building Destruction

When building HP reaches 0:

* building becomes destroyed
* building stops functioning
* occupied navigation cells are eventually released
* destruction visual may play

If the destroyed building is the Castle:

end the match immediately.

---

# 33. Factions

Initial factions:

Blue

Red.

A faction contains:

* resource totals
* population
* units
* buildings
* Castle
* Commander

Faction logic should not depend on sprite color.

Faction color is visual presentation only.

---

# 34. Player and Enemy

Initial game mode:

Human Player = Blue.

Enemy = Red.

Red will initially use a Rule-Based Commander.

Reinforcement Learning will replace or compete against the Rule-Based Commander later.

---

# 35. Rule-Based Enemy

Before RL is implemented, the enemy must be capable of playing the game using scripted rules.

Basic goals:

1. maintain enough Pawns
2. gather resources
3. build Houses when near population cap
4. build Barracks
5. train military units
6. react to enemy army composition
7. defend when attacked
8. attack when army strength is sufficient

Example strategic reaction:

If the enemy invests heavily in Warriors:

consider producing multiple Lancers.

The Rule-Based AI should respect the intended combat relationships defined in this document.

This AI exists to verify that the game is playable before RL integration.

---

# 36. Game Speed

The default game is real time.

Initial target speed:

1x.

Optional later speeds:

2x

4x

Training mode may run much faster than normal gameplay.

Game simulation must not depend on visual frame rate.

---

# 37. Camera

Player camera supports:

Arrow-key movement, including normalized diagonal combinations.

Mouse-edge scrolling in all eight directions with a configurable edge width.

Mouse wheel zoom.

Camera cannot move infinitely outside the playable map.

WASD is not used for camera movement; `A` is reserved for AttackMove.

---

# 38. UI

Minimum UI must display:

Food

Wood

Gold

Population / Population Cap

Selected units

Selected building

Available actions.

Optional:

Minimap.

Minimap is not required for MVP.

Phase 6D.3 command tooltips are presentation only. Train commands obtain costs,
population, combat stats and training duration from `UnitCombatData`; military
strengths and weaknesses come from `CombatDamage.Multiplier`. Build commands
obtain costs, HP, logical footprint, construction duration, population bonus
and defense capacity from `BuildingData`. Tooltips never spend resources,
reserve population or decide whether a command is legal. Disabled Train and
Build commands remain inspectable.

Explicit target modes have first cursor priority. Building placement and
AttackMove keep their context validity cursor. With no explicit target mode,
an observable friendly selectable entity uses the existing Hand cursor and
ordinary terrain uses the Default cursor. A selected production building does
not implicitly enter a rally targeting mode; right click remains the command
that attempts to set its rally point. Fog and UI blocking continue to run
before world hover feedback.

---

# 39. Match Statistics

The game should track:

match duration

winner

resources collected

units trained

units lost

enemy units destroyed

buildings constructed

buildings destroyed

damage dealt

damage received.

Statistics should also track unit composition when practical.

These statistics will later be useful for Reinforcement Learning evaluation.

---

# 40. Game Rules Important for RL

The simulation must be deterministic where reasonably possible.

RL should later control high-level strategy.

RL must NOT directly control every unit's exact movement.

Low-level systems remain controlled by standard game logic:

* navigation
* pathfinding
* gathering
* combat execution
* animation
* building construction

RL Commander decides strategic actions.

The RL agent should be able to learn relationships such as:

* Warriors are powerful but expensive
* one Lancer does not defeat one Warrior
* multiple Lancers are an effective response to Warriors
* Archers punish Lancers
* Warriors punish Archers

Exact RL design belongs in:

RL_SPEC.md.

---

# 52. Phase 6C.2 Monk and Strategic Gold Mine

Monk is a support unit trained by Monastery. Provisional MVP values are 80 HP,
1 population, speed 3, 60 Food + 60 Gold and 7 seconds training. Monk has no
normal attack or retaliation. It restores 5 HP every 4 seconds at range 2.
Automatic healing detects injured same-faction units within range 4 and chooses
lowest HP ratio, then nearest distance, then stable registry order. Manual
right-click healing has no acquisition-distance limit and ends when resolved or
replaced by another explicit command.

The central Gold Mine is a neutral 2x2 logical blocker with 1500 HP and 2000
Gold. The first Blue or Red Pawn to enter claims it; ownership persists while
empty. Up to three internal Pawns generate 15 Gold each every 5 seconds. A
partial final payout uses only remaining Gold. Depletion safely ejects Pawns and
leaves the destroyed visual without an explosion. Combat destruction kills only
Pawns currently internal, uses normal death authority and leaves the same wreck.
Its Fire threshold is 0.60, shared with ordinary completed buildings.

The strategic Gold Mine uses a uniform presentation scale of 2.60 for inactive,
active and destroyed states. A dedicated visual root adds a +0.92 world-unit Y
ground-contact correction; the authoritative 2x2 footprint and 1.75x1.25
interaction collider remain unchanged. Its enlarged Fire presentation uses
scale 2.47 with anchors (-0.585, 0.13), (0, 0.546), (0.585, 0.156). Its building
Explosion scale is 2.08. The current 60x44 map places the Gold Mine on flat
central ground, with its 2x2 footprint beginning at (29,21). Both main Castles
stand on height-2 upper courts (10x7), nested within spacious 16x13 height-1
terraces. Each base has two independent side routes, each with two ramp flights.
Single cliff rows are separated by a walkable landing. Each visible Castle ramp and its exact logical height
transition come from one shared connector definition. Sheep graze above and
below the Castle land instead of occupying its construction area.

Goblin is a neutral hostile faction outside match-result, economy and population
systems. One Torch and one TNT guard the primary mine. Torch uses Warrior
baseline stats and regenerates approximately 1 percent MaxHP per second after
five seconds without damage. TNT provisional values are 80 HP, 18 damage,
range 4.5, interval 2.5, explosion radius 1.25 and deterministic knockback 0.60.
TNT AoE affects Blue/Red units once, never Goblins, buildings or the mine.
Goblin presentation uses the verified 192x192 exported PNG cells documented in
`ASSET_MANIFEST.md`; transparent row-padding cells are excluded from animation
clips. TNT Throw is seven frames at 10 FPS and releases the projectile at the
start of zero-based frame 6 (0.60 seconds). This presentation timing does not
change its 2.5-second gameplay attack interval or any damage, range, radius or
knockback value.

Current UI/UX milestone scope: Goblin Torch and Goblin TNT are temporarily
disabled in the active Map setup, runtime spawn flow, current objective and
automated validator. Their verified assets, data and dormant implementation are
retained. Re-enabling them is a separate future gameplay task.

---

# 41. MVP Scope

The first complete playable version contains only:

1 map

2 factions

1 Castle per faction

Pawn

Warrior

Archer

Lancer

Tree

Sheep

Gold Rock

House

Barracks

Archery

Monastery (building only until Monk gameplay is defined)

Tower

resource gathering

building construction

unit production

combat

population

win / lose condition

rule-based enemy AI.

---

# 42. Out of Scope

Do NOT implement these unless explicitly requested:

* technology tree
* Ages
* civilization bonuses
* heroes
* equipment
* inventory
* naval units
* ships
* trade
* diplomacy
* walls
* gates
* multiplayer networking
* procedural maps
* campaign
* quests
* experience / leveling
* unit upgrades
* weapon upgrades
* advanced formations

These are future features.

---

# 43. Design Priorities

Priority order:

1. Correct gameplay
2. Stable architecture
3. Easy debugging
4. RL compatibility
5. Visual polish

Do not sacrifice gameplay correctness for animation or visual effects.

Combat balance goals are more important than exact initial stat values.

If testing shows that the intended matchup relationships are incorrect, numerical stats may be adjusted while preserving the intended design.

---

# 44. Phase 5C2.1 Construction and Economy Continuity

Construction uses additive MVP BuildPower. Every Pawn that has physically
reached its own valid reserved interaction cell and is actively building
contributes BuildPower 1.0. The authoritative construction rate is the base
rate multiplied by total active BuildPower. Resource cost is still paid once
at placement. Interrupting one builder removes only that Pawn's contribution;
the other builders continue and accumulated progress never resets.

A Pawn keeps the ResourceType of its active gathering job when a node is
depleted or its requested interaction slots are full. It first searches for the
nearest currently visible same-type node with reservable capacity. It may then
travel to the frozen position of a same-type resource previously captured by
that faction's Fog memory. Unexplored resources and hidden live amount/state are
never queried for target choice. A remembered target is resolved to a live node
only after the Pawn reveals it. Candidate ordering uses distance then EntityId.
Full cargo returns to the nearest reachable completed friendly Castle or Tower.
Partial cargo continues across same-type nodes; if no legal known target exists,
the Pawn deposits that cargo and becomes Idle. Any explicit player command ends
the automatic chain.

Gold Stone capacities are gameplay data and do not depend on sprite size:

* Gold Stone 1: 100 Gold
* Gold Stone 2: 150 Gold
* Gold Stone 3: 250 Gold
* Gold Stone 4: 300 Gold
* Gold Stone 5: 400 Gold
* Gold Stone 6: 500 Gold

# 45. Military Retaliation

Warrior, Archer and Lancer may retaliate when they receive damage from a valid
enemy unit. Pawn and Monk do not automatically retaliate. Damage carries the
original attacking `UnitController`; projectiles preserve that source through
impact and the projectile object itself never becomes the retaliation target.

Retaliation can acquire the attacker outside the normal passive acquisition
radius. It uses the existing CombatController, A*, ranged positioning and melee
engagement slots. Its configurable provisional MVP leash is 10 world units,
measured from the position where retaliation began. If either combatant exceeds
that leash, the attacker dies, becomes protected, becomes unreachable or is no
longer targetable, retaliation ends safely.

Explicit player Attack, Guard and normal Move remain authoritative. A unit
already fighting a valid target keeps it rather than switching on every hit.
AttackMove may be interrupted by retaliation and resumes its stored destination
after that target is resolved. Automatic retaliation never creates the red
player Attack-command feedback rings. Internal and mounted protected occupants
do not retaliate because they cannot receive valid direct unit damage.

# 46. Phase 6A Vision Balance

Unit vision is configured in `UnitCombatData` and remains separate from attack
range and aggro radius:

* Pawn VisionRadius = 3.0
* Warrior VisionRadius = 3.5
* Archer VisionRadius = 4.0
* Lancer VisionRadius = 3.5

Archer intentionally has slightly better vision than melee units. Strategic
buildings retain their larger values: Tower VisionRadius = 7 and Castle
VisionRadius = 9. Vision calculations, per-faction exploration and Fog of War
presentation are defined in `VISION_FOG_SPEC.md`.

# 47. Phase 6B Combat Awareness

Normal combat awareness uses the authoritative faction visibility stored by
`TeamVisionSystem`. Passive acquisition, AttackMove interruption, explicit
Attack continuation, mounted Archer targeting and Tower/Castle defensive
targeting require the enemy's current gameplay position to be Visible to the
attacker's faction. Shared allied vision counts; attack range, aggro radius and
VisionRadius remain separate values.

When a normal target leaves faction vision, the attacker clears the target,
cancels any unreleased ranged windup and releases its melee engagement slot.
AttackMove resumes its saved route. Other normal orders return to their existing
Idle or defensive-anchor behavior. Phase 6B does not pursue a last-known
position. AttackMove continues to acquire units only and does not add automatic
building acquisition.

A projectile released while its target was legitimately visible remains
authorized and resolves through the existing projectile rules even if the
target later leaves vision. A ranged windup that has not released its projectile
is cancelled when awareness is lost. Each later attack cycle and each defensive
building volley must pass a new visibility check.

Direct damage provides a controlled local retaliation exception for Warrior,
Archer and Lancer. Only the damaged unit may track that specific attacker while
the existing validity rules and 10-world-unit RetaliationLeash hold. This local
awareness never changes `TeamVisionSystem`, reveals the attacker, enables player
interaction with it, or lets other friendly units acquire it. Pawn and Monk do
not auto-retaliate. Explicit Attack, Guard, normal Move and current-target
priority remain unchanged.

# 48. Phase 6B.1 Building Rally Points

Completed friendly Castle/Home, Barracks, Archery, Monastery and Tower may each
store one independent Rally Point. House is excluded. A Rally
destination must be valid, reachable logical ground in a `Visible` or
`Explored` cell; `Unexplored`, outside-map, blocked, Water and Cliff destinations
are rejected without replacing the existing point.

Produced units first spawn at the normal safe BuildingSpawnPoint and then
receive a normal Move toward a distributed destination around their producer's
Rally Point. They are never spawned directly at the Rally Point. Manual Tower
ungarrison/Eject All follows the same rule after safe nearby ejection. Emergency
ejection caused by building destruction ignores Rally and leaves survivors
Idle. The Rally flag is selection-only presentation with no collider, raycast,
navigation or vision effect.

# 49. Phase 6B.2 Movement and Building Damage Presentation

Movement and attack facing are presentation state on the unit's visual
SpriteRenderer. Meaningful horizontal movement faces the sprite left or right;
standing preserves the last direction. Attacks face the current unit/building
target, and Pawn work faces its resource or construction target. Gameplay roots,
colliders, selection rings, health bars and VFX are never mirrored. Lancer keeps
its Down, DownRight, Right, Up and UpRight attack selection and uses horizontal
flip only for the corresponding left-side directions.

A successful AttackMove command creates a fixed two-ring red world marker at
the ground destination. It shrinks and fades independently of the red explicit
Attack marker that follows an enemy. Automatic acquisition and retaliation do
not create either player-command marker.

A completed damageable building burns while `0 < CurrentHP / MaxHP < 0.60`.
Repair at or above 60 percent stops the three looping presentation-only Fire
effects; under-construction buildings never burn from construction HP semantics.
At zero HP, gameplay destruction remains immediate, looping Fire stops and the
two verified Explosion effects play once as an independent one-shot. Enemy Fire
and Explosion presentation never bypasses Fog visibility.

Damaged completed buildings use the Tiny Swords BigBar Life Bar: one static
assembled frame and one continuous red fill cropped from the right while its
left edge and height remain fixed. It hides at full HP and on destruction.
Units continue using SmallBar, and construction progress remains separate.

# 50. Phase 6C Match Flow, Victory and Defeat

`MatchManager` is the authoritative match lifecycle service. Its state advances
from `Preparing` to `Playing` after the explicitly registered Blue and Red Main
Castles are available, then to `Ended` when the first Main Castle destruction
event is received. The only current win condition is destruction of the enemy
Main Castle. Destroying House, Barracks, Archery, Monastery or Tower does not
end the match.

The result is `BlueVictory` when the Red Main Castle is destroyed and
`RedVictory` when the Blue Main Castle is destroyed. Blue is the current human
faction, so these map to VICTORY and DEFEAT respectively. The first authoritative
Main Castle death locks the result; repeated or same-frame later callbacks are
ignored and no Draw result is introduced.

Match result authority changes immediately at Castle death. Normal Castle
destruction, garrison ejection and Fire/Explosion cleanup still run. New player
commands, production, gathering, construction, repair, garrison orders,
retaliation, automatic combat and defensive volleys stop once the state is
`Ended`. Already released projectiles and presentation VFX may finish. Camera
pan and zoom remain available. `Time.timeScale` is not used for correctness.

The screen-space result UI appears after a presentation-only 0.85-second delay,
clears and disables selection, hides gameplay command panels and blocks world
pointer input. Restart reloads the active gameplay scene so economy, population,
units, buildings, Fog history, queues, garrison and match state reset together.
Future Commander and RL systems query `IsMatchEnded`, `WinningFaction`,
`LosingFaction` and the match-ended event directly rather than reading UI text.

# 51. Phase 6C.1 Fog Memory and Presentation Rules

Gameplay death remains immediate at zero HP. The shared unit death presentation
then plays all fourteen verified `Dead.aseprite` frames in order: Dead 0-6,
followed by Buried 7-13, and destroys only the presentation object after its
1.8-second sequence. Pawn, Warrior, Archer and Lancer of either faction use the
same presentation path. No permanent corpse remains.

Authorized Warrior and Lancer attacks use the same `AttackPerformed`
presentation event for unit and building targets. Building direction is measured
to the closest combat collider point. Warrior uses its verified attack; Lancer
retains directional attacks and horizontal flip for left-side equivalents.
Damage, cooldowns and combat stats are unchanged.

The main Castle/Home supports the same stored Rally Point as the other output
buildings. A produced Pawn first spawns at a safe Castle output cell and then
receives a normal Move. Voluntary Castle ejection follows Rally after safe
ejection; emergency destruction ejection does not. Rally Point rules remain
Visible or Explored allowed and Unexplored rejected.

World truth, per-team visibility and faction memory remain separate. Explored
building/resource visuals are last-seen presentation snapshots and never live
targets. Hidden physical occupancy stays authoritative for A*, but cannot alter
player cursor or command acceptance before the obstacle is currently observed
or remembered by that faction. Enemy units receive no last-seen ghost.

Health presentation is relative to `PlayerViewFaction`. Friendly unit SmallBar
and building Life Bar fills are green (`Color32(74, 201, 86, 255)`); enemy fills
are red (`Color32(255, 62, 62, 255)`). Frames and backgrounds retain their
original art. Hidden live enemies expose no bar, and an Explored last-seen
building ghost never contains health, Fire, repair, garrison or production UI.

Every positive unit HP loss overlays a presentation-only, ground-anchored
squash, stretch and recovery on the dedicated visual body. It does not change
the gameplay root, collider, movement, facing, animation state or combat timing.
The Phase 6C.1 MVP values are `SquashScaleY = 0.84`, `StretchScaleY = 1.12`,
`SquashDuration = 0.055` seconds, `StretchDuration = 0.065` seconds and
`RecoverDuration = 0.08` seconds. A new hit restarts from the stored baseline;
lethal gameplay death remains immediate. Each successful Sheep Food transfer
triggers the same deformation alongside the existing harvest flash without
turning gathering into combat damage.

# 52. Phase 7A Rule-Based Commander Foundation

Red is controlled by one Rule-Based Commander while Blue remains human-controlled.
The Phase 7A Commander only balances Pawn gathering and assigns one scout. Its
default worker targets are Food 40%, Wood 35% and Gold 25%, with deterministic
small-roster allocations of 1/1/1 for three workers and 2/1/1 for four workers.
An active Pawn scout is excluded from economy counts.

Commander decisions run every 0.50 seconds and use `FactionKnowledge`. Own-faction
state is authoritative. Enemy units exist in observations only while currently
Visible. Buildings and resources may persist only as copied last-seen snapshots;
hidden live state and live object references are unavailable. Scouting chooses Red
exploration frontiers with priority Lancer, Warrior, Archer, Pawn and never Monk.

All AI actions use `FactionCommandService`. Phase 7A exposes Move and Gather and
enforces match state, ownership, actor, visibility and reachability validation.
The Commander stops issuing commands outside `MatchState.Playing`. Construction,
production, military strategy, Gold Mine contest logic, Goblin AI and RL remain
deferred. Detailed contracts are in `AI_SPEC.md`.

# 53. Phase 7B Rule-Based Development

The Red Rule-Based Commander may construct Houses, one Barracks, one Archery and
one Monastery, train units through normal production queues and set Rally Points.
It uses the existing authoritative `BuildingData`, `UnitCombatData`,
`BuildingSystem`, `FactionPopulation` and `UnitProductionQueue` values.

The Phase 7B configurable targets are six Pawns and eight non-Pawn military
population. Effective free population includes reserved queue population. House
construction is requested at two or fewer effective free slots; its normal +5
population bonus applies only on gameplay completion. The AI keeps one strategic
construction project and one desired queued item per producer at a time.

The starter military composition targets three Warriors, two Archers, two
Lancers and one Monk, counting living plus queued units and limiting Monk to one.
Military production Rally Points use explored safe ground near Red's base. The
Commander does not issue military attack orders in Phase 7B. Tower strategy,
Gold Mine contest strategy, Goblins and full military behavior remain deferred.

# 54. Phase 7C.1 Defensive Army Group

The Red Rule-Based Commander organizes available Warrior, Archer, Lancer and Monk
units into one defensive Army Group. Pawns, dead or stationed units and the active
Scout are excluded. The group waits around the existing Phase 7B military staging
area and transitions through Forming, Ready, Defending and Regrouping.

The Red Main Castle footprint center anchors a provisional 9-world-unit base
defense radius and 12-world-unit defense leash. The Commander may target only a
currently Red-visible hostile playable unit. Priority is a unit attacking the
Castle, another Red building, or a Red unit, followed by other hostile military
and then a Pawn. Current visible targets remain stable until invalid, dead, hidden
or beyond the leash.

Warrior, Archer and Lancer use the normal authoritative Attack command. Monk moves
with the group for its existing support behavior and is never issued Attack. When
danger ends, survivors receive distributed normal Move commands back to staging.
The Army Group never launches an offensive, attacks remembered buildings, targets
the Blue Castle, contests the Gold Mine or enters Unexplored territory in this
phase. Phase 7C.2 owns offensive behavior.

# 55. Phase 7C.1A Strategic Safety

The Red Rule-Based Commander treats `HomeEconomicRadius = 11` as the provisional
normal operating radius around its Main Castle on the 40x28 test map. Economy
resources must be legally known, not known depleted, inside that radius, on
explored safe ground and reachable through safe explored cells. Economy ratios are
targets; unavailable Food never authorizes a cross-map civilian expedition. Safe
other resource types may be used before the Pawn remains idle/available.

Knowledge-based danger comes from currently visible hostile units and legitimately
observed current or remembered hostile buildings. Tower/Castle danger uses their
recorded defensive range plus 1.5 world units. Visible Warrior/Archer/Lancer danger
uses recorded attack range plus a 2.5-unit reaction buffer. Hidden units contribute
no continuing position danger. Pawns retreat from newly unsafe work; Scouts retreat
or route around known danger. This hotfix adds no offensive army behavior.

# 56. Phase 7C.2 Offensive Strategy

The Red Rule-Based Commander may begin one offensive only after its living,
available, non-Scout and non-garrisoned Army Group reaches the provisional
`MinOffensiveMilitaryPopulation = 6`. At least 75 percent of the operation's
members must assemble in the existing military Rally staging region before launch.
Queued units do not count. Units produced after an operation starts remain at the
base staging area and join a later offensive.

The strategic states are `Forming`, `Ready`, `PreparingAttack`, `Advancing`,
`Assaulting`, `Defending` and `Regrouping`. A serious currently visible threat to
the Red base interrupts PreparingAttack, Advancing or Assaulting. The operation is
cancelled, normal Phase 7C.1 defense runs, and survivors regroup before a later
fresh evaluation.

Strategic objectives come only from Red `FactionKnowledge`: currently visible
enemy buildings or frozen last-seen building snapshots. A remembered hidden
building supplies a destination for normal authoritative AttackMove; it is never a
direct Attack target. `FactionCommandService.TryAttackBuilding` resolves a live
building only after the EntityId is in current Red-visible building observations
and TeamVision confirms visibility again. Re-revealing an empty remembered area
clears that stale objective.

Target selection is deterministic: known Tower, known Barracks/Archery/Monastery,
other useful known building, then known Main Castle; risk, squared staging distance
and EntityId break ties. A selected objective persists until confirmed gone,
invalid, repeatedly unreachable or interrupted by defense. The army uses distributed
AttackMove destinations, command intent deduplication and a bounded failure retry.
It may cross known Tower/Castle danger with an Army risk profile; those regions are
costly rather than forbidden. Local AttackMove combat remains authoritative.

Monk receives support Move orders behind the operation centroid and never receives
Attack. The active Scout stays outside the Army Group and may continue legal
frontier exploration. After completion or failure, survivors return to the existing
Red staging area and wait through a provisional three-second strategic reevaluation
delay. Phase 7C.3 below adds combat power and strength/casualty retreat. Counter-unit
production, multiple squads, flanking, focus fire, advanced Monk micro, Gold Mine
warfare and special final-Castle tactics remain deferred.

# 57. Gameplay SFX / Phase 6E.2

Gameplay audio is presentation-only and reacts after authoritative actions. It
must never change combat cooldowns, projectile timing, healing, gathering,
construction, repair, production, match results, resource depletion or AI decisions.
Supported world events are Warrior/Lancer successful melee attacks, Archer projectile
launch, successful Monk heal, successful Pawn Gold/Wood impacts, Pawn hammer work,
building fire and observable building destruction. Sheep Hurt remains disabled.

World/UI one-shots start with twelve pooled AudioSources. When all are busy, the pool
grows by four and reuses the expanded sources. Legitimate instances of the same event
may overlap without a per-event cap, same-clip cooldown or duplicate suppression. A
256-source global catastrophic guard exists only to diagnose erroneous frame-spam.
Fire uses separately owned loop handles so multiple damaged buildings may burn at once.
Master SFX volume defaults to 1.0; per-event volume, playback cap and optional
0.96-1.04 pitch variation remain catalog presentation data.

The AudioListener remains on the Main Camera. World SFX uses full 3D spatial blend
with linear distance rolloff: actions directly below the camera center are loudest,
and become progressively quieter as the camera center moves away from the source.
A world SFX request is accepted only while its source point is inside the Main
Camera viewport. An active clip stops if camera movement takes its source outside
the viewport. Fog visibility rules remain an additional requirement.

Own-faction world SFX remains eligible. Enemy and neutral SFX is eligible only if
the source position is currently visible to the local player's faction through
TeamVisionSystem, preventing audio from revealing hidden actions through Fog. Fire
starts below 60% HP, stops at or above 60%, stops when an enemy building leaves Blue
vision, and ends before its one observable destruction sound.

Building selection, command accepted/rejected, successful building-plan placement,
Human unit production completion and MatchManager victory/defeat are 2D player
feedback. Building buttons use authoritative availability/results, so `click` and
`cant_click` never both play for one click. Match result audio is once per match and
restart resets that guard. Food harvesting, generic hit/death, ambience, attack-theme
music and mixer/options UI remain future work. `AUDIO_SPEC.md` owns the exact mapping.

# 58. Phase 7C.3 Combat Strength, Retreat and Recovery

The Red Army strategic states are `Forming`, `Ready`, `PreparingAttack`,
`Advancing`, `Assaulting`, `Defending`, `Retreating`, `Recovering` and
`Regrouping`. Combat assessment runs only on the 0.50-second Commander tick during
an active advancing/assaulting campaign.

Provisional strength values are Warrior 1.00, Archer 0.75, Lancer 0.60 and Monk
support 0.45. Unit effective strength is base value multiplied by current HP / Max
HP. Pawn does not count. A currently visible local Tower contributes 2.00 and Castle
3.00, health weighted. The assessment radius is 9 world units around the campaign
centroid and uses only current Red-visible copied observations.

Campaign launch records its own participant population, count, strength and average
HP. Reinforcements do not change that baseline. Retreat triggers immediately below
0.45 remaining campaign population, or after two consecutive evaluations below
0.35 average HP or above a 1.40 visible-enemy/own-strength ratio. Hidden enemies,
remote Scout contacts and hidden live defensive-building state cannot contribute.

Retreat cancels the current offensive intent, applies a generic Retreat combat
directive and sends surviving participants to distributed staging positions. It
suppresses proactive target acquisition, retaliation and chase while units remain
vulnerable to normal damage. Retreat completes when 75 percent of surviving
participants are staged or after a provisional 12-second straggler timeout.

Recovering requires three seconds, six military population, 75 percent staging
cohesion, and either average HP 0.65 or effective strength 4.0. Full HP and Monk are
not required. A ten-second ceiling releases an otherwise ready staged Army when no
healing source exists, preventing permanent recovery deadlock. Economy, production
and Scout continue. New units remain at Rally during retreat and join recovery at
home. A failed retreat objective is skipped for 12 seconds before normal legal
target selection may reconsider it.

# 59. Phase 6D.4 Interaction Presentation

Normal cursor presentation resolves an observable selectable entity before ground
movement validity when no explicit target mode is active. Sheep, Trees, Gold
resources, Gold Mine and buildings may use sprite-bound trigger colliders solely
for pointer queries. Resource hit areas are derived by converting the current
SpriteRenderer world bounds into the hit-area transform's local collider space and
are resynchronized when animated resource frames change. Sheep alone adds a small,
symmetrical 0.08-world-unit pointer padding around those visual bounds so cursor
hotspot placement remains usable at visible wool/body edge pixels. Existing gameplay
colliders, logical footprints, grid occupancy, pathfinding blockers, interaction
slots and combat targeting remain authoritative. UI blocking, Placement and
AttackMove retain higher cursor priority. Fog-hidden objects never produce
selectable hover feedback.

Every assigned Pawn becomes an active construction contributor only after reaching
its reserved interaction position and entering `PawnBuilder.IsWorking`. All active
contributors loop the verified Hammer animation; walking or waiting builders do
not. A 0.2-world-unit arrival tolerance accommodates the small terminal displacement
that UnitSeparation may apply when a path completes at a reserved slot; builders
still require movement completion and their own valid reservation before working.
Construction progress continues to be added by the existing PawnBuilder gameplay
update and never by animation frames or events. Builder removal, death,
reassignment and completion stop the presentation through the existing job state.
Repair continues to use the same working animation and authoritative repair impact.

# 60. Phase 6G.1 Role-Based Army Movement

Human multi-unit Move and ground AttackMove commands arrange final destinations in
a compact formation oriented from the selected units' ground-position centroid
toward the clicked point. The local forward axis points toward the command and the
lateral axis is perpendicular to it, so the layout supports arbitrary directions.
A near-zero direction deterministically falls back to world right.

The canonical tactical priority is Warrior, Lancer, Archer, Monk and Pawn.
Missing roles leave no empty slot. Future or non-player unit types use a safe
middle/rear priority. Compact slots use a default one-cell spacing and are ordered
from the leading row toward the rear; roles receive the earliest available slots
in canonical order. Roles may share a row when its geometry has several positions.
Within a role, deterministic nearest-pair matching minimizes travel and uses stable
EntityId order to resolve ties.

Desired geometry is corrected through the existing logical grid destination
distribution. Final cells must be unique, walkable, unoccupied and on the clicked
cell's logical height. Slot scoring performs no path searches; each assigned unit
uses its existing A* request only when the command is issued. Single-unit commands
retain their previous clicked-destination behavior. Formation is an arrival layout,
not a rigid leader/follower system: A*, UnitSeparation and combat behavior remain
authoritative during travel and engagement. Phase 6G.1 changes only Human group
commands; RuleBasedCommander movement keeps its existing APIs and assignments.

# 61. Phase 6G.2 Player Formation Commands and Shapes

The Human command state exposes exactly four `FormationType` values: `Compact`,
`Wedge`, `Loose` and `Line`. Compact is the match-start default, and the latest
selection persists in `PlayerCommandController` when the player changes unit
selection. There is no Column or SingleColumn formation.

Formation type generates role-agnostic desired geometry around the command anchor.
The existing Phase 6G.1 role priority then assigns units to those tactical slots,
and the existing bounded logical-grid correction resolves each desired point to a
unique walkable, unoccupied cell at the command height. UnitMovement, A*,
UnitSeparation and combat remain authoritative after command issue. Formation is
recalculated only for an explicit formation choice, Move or ground AttackMove;
combat is free to break it and no Update loop continuously restores it.

Player-selectable Phase 6G.2 formations are military-only. The single canonical
eligibility rule explicitly admits Warrior, Lancer, Archer and Monk; Pawn and any
unknown/future unit type are excluded. Formation buttons require at least two
living, friendly, controllable eligible military units. Selected Pawns never enter
formation slot generation, role assignment, centroid calculation or reform-in-place,
and a formation-button click never moves them or cancels their economy jobs.

Compact uses a tight adaptive multi-row block at one-cell spacing. Wedge starts
with one leading slot and widens each rear row by one; incomplete final rows are
centered. Loose uses the Compact block with a provisional 1.65 spacing multiplier
on both axes. Compact, Wedge and Loose order tactical slots from front to rear, so
Warrior, Lancer, Archer and Monk receive progressively later slots without
reserving empty role rows.

Line is exactly one lateral rank perpendicular to the group forward axis. Odd
counts contain one center slot; even counts use symmetric half spacing around the
center. Its tactical slot order is center-to-wing, so canonical role priority puts
Warriors nearest the center, followed by Lancers, Archers and Monks toward the
wings. It never creates a second row merely because the selected group is
large. All four shapes share the same normalized forward axis from group centroid
to command point and perpendicular lateral axis, including diagonal movement.

Selecting a formation with at least two valid eligible military units immediately
issues a normal reform move around their military-only centroid. It uses the last
meaningful group movement direction, or deterministic world right before one has
been established. Selecting a different shape while moving replaces the units'
previous destinations through the normal latest-command path. Single-unit movement
and direct target Attack keep their previous behavior.

A normal ground Move remains an explicit command for every selected controllable
unit. With a mixed selection, the military subset uses the selected formation and
Pawns use the existing nearby logical destination distribution outside the cells
reserved by the military formation. Pawns do not influence the military centroid
or facing, but their normal Move still cancels their previous job as it did before.
Ground AttackMove preserves its existing combat-unit eligibility and applies the
formation only to formation-eligible military participants.

# 62. Phase 6H.1 Economy and Worker Logistics

Pawn carry capacity is 20. Each successful 1.5-second work cycle transfers up to
2 units at the existing 0.45-second gameplay impact, capped by both remaining
resource amount and free cargo space. A Pawn carrying 19 therefore takes exactly
1 and leaves the unused unit in the node.

`IResourceDropOff` and its registry are the shared authority for Castle and Tower
deposits. Selection sorts valid same-faction, living, completed candidates by
cheap squared distance and stable EntityId before requesting a reachable reserved
slot. An invalidated destination is released and recalculated without losing
cargo. Deposits continue to update the faction's one authoritative stockpile.

Resource overflow and depletion continuation are transition-driven rather than
per-frame searches. Visible candidates require a real free interaction slot.
Hidden candidates come only from immutable faction Fog snapshots and are approached
through explored cells; their current amount, movement and existence remain hidden
until re-observed. Rule-Based AI uses the same Pawn mechanics and safety position
filter without gaining a second knowledge path.

# 63. Phase 6H.2 Navigation and Sheep Interaction

Authored height connectors remain reciprocal diagonal edges. Runtime movement
consumes each connector endpoint separately, constrains local unit separation
to the connector axis during that short traversal, and validates the reciprocal
pair directly instead of classifying the connector's exact shared corner as an
orthogonal cliff cell. A 0.75-second no-progress watchdog may request one
throttled recovery route; normal traversal never teleports.

The lower and upper endpoint cells are a two-cell transit-only corridor. They
remain pathable but cannot be reserved as a gather, build/repair, garrison,
drop-off, production spawn, rally-issued/formation or other persistent standing slot.
Building footprints cannot overlap either endpoint. Nearby same-height ground
remains available when it satisfies the existing occupancy and reachability rules.

Sheep are selectable Food resources with trigger-only interaction coverage and
soft navigation occupancy. Units and A* may pass through them. Sheep alone do
not invalidate an otherwise legal building footprint. After successful placement,
every Sheep physically or logically inside the footprint receives a deterministic
destination on the nearest bounded walkable ring outside it; destinations are
distinct where possible and construction occupancy remains authoritative for units.

# 64. Source of Truth

This file defines gameplay rules.

If code conflicts with this document:

report the conflict.

Do not silently change gameplay rules.

The following balance rule is particularly important:

1 Warrior should defeat 1 Lancer.

2 Lancers should defeat 1 Warrior.

Do not change this relationship without explicitly updating GAME_SPEC.md.

If the requested feature would modify an existing rule:

update GAME_SPEC.md before or together with the implementation.

Asset usage belongs in:

ASSET_MANIFEST.md.

Terrain and navigation rules belong in:

MAP_SPEC.md.

Reinforcement Learning rules belong in:

RL_SPEC.md.
