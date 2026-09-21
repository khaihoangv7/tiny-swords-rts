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

Pawn is not intended to be a combat unit.

Cost:

50 Food.

Carry capacity:

10 resource units.

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

Move to resource

→ Gather

→ Inventory reaches carry capacity

→ Return to Castle

→ Deposit resources

→ Return to resource

→ Continue gathering.

If the resource is depleted, the Pawn becomes Idle.

---

# 8. Unit Types

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

The initial game contains:

* Castle
* House
* Barracks
* Tower

No other building types are required for MVP.

---

# 16. Castle

The Castle is the main faction building.

Functions:

* determines win / lose condition
* acts as resource drop-off point
* represents the faction headquarters

If Castle HP reaches 0:

that faction loses immediately.

---

# 17. House

Cost:

100 Wood.

Function:

Increase population capacity by 5.

The player may build multiple Houses.

---

# 18. Barracks

Cost:

150 Wood.

Function:

Train military units.

Available units:

* Warrior
* Archer
* Lancer

Each training action requires resources and time.

Only one unit should be produced per training request.

---

# 19. Tower

Cost:

100 Wood

50 Gold.

Function:

Automatically attacks enemy units inside its attack range.

Tower target priority:

nearest valid enemy unit.

Tower cannot move.

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

Build

Repair

ReturnResource

---

# 24. Move Order

Right-clicking walkable ground gives selected units a Move order.

Units pathfind toward the destination.

Units stop when reaching an acceptable destination radius.

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

---

# 26. Attack Move

AttackMove means:

Move toward destination.

If an enemy is detected:

temporarily interrupt movement

→ chase enemy

→ attack

→ when enemy dies, continue toward the original destination.

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

WASD movement

Mouse edge movement may be added later.

Mouse wheel zoom.

Camera cannot move infinitely outside the playable map.

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
* fog of war
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

# 44. Source of Truth

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
