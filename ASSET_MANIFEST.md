# Tiny Kingdom RL — Asset Manifest

## Purpose

This file defines how Tiny Swords visual assets correspond to gameplay entities and gameplay states.

Codex must read this file before:

* creating Animator Controllers
* creating Animation Clips
* creating unit prefabs
* modifying unit visuals
* modifying resource visuals
* modifying building visuals

DO NOT guess animation usage from filenames alone.

When an exact source filename or frame range is unknown, inspect the imported Tiny Swords asset first and update this document before implementing the animation.

---

# 1. General Animation Rules

Tiny Swords animation speed:

10 FPS

Default animation frame time:

0.1 seconds per frame.

Gameplay state is the source of truth.

Animation must reflect gameplay state.

Animation must NEVER determine gameplay logic.

For example:

Incorrect:

Animation finishes → resource is collected.

Correct:

Gathering system determines when resource is collected.

Animation only visually represents Gathering.

---

# 2. Pawn

Gameplay entity:

Pawn

Purpose:

Worker responsible for economic activities.

Pawn gameplay activities:

IDLE
MOVE
GATHER_WOOD
GATHER_GOLD
GATHER_FOOD
BUILD
REPAIR
DEAD

---

## IDLE

Gameplay condition:

Pawn has no active order.

Animation:

Pawn Idle

Loop:

YES

---

## MOVE

Gameplay condition:

Pawn is moving toward a destination.

Animation:

Pawn Run / Walk animation supplied by Tiny Swords.

Loop:

YES

---

## GATHER_WOOD

Gameplay condition:

Pawn is gathering a Tree resource.

Target type:

TREE

Required visual:

Tiny Swords Pawn wood chopping animation.

Loop while gathering:

YES

Gameplay relationship:

Tree → Wood

Do not use generic attack animation for wood gathering when a dedicated chopping animation exists.

---

## GATHER_GOLD

Gameplay condition:

Pawn is gathering a Gold Rock resource.

Target type:

GOLD_ROCK

Required visual:

Tiny Swords Pawn mining animation.

Loop while gathering:

YES

Gameplay relationship:

Gold Rock → Gold

Do not reuse the wood chopping animation.

---

## GATHER_FOOD

Gameplay condition:

Pawn is gathering food from the food source defined by the game.

Initial food source:

SHEEP

Required visual:

Use the dedicated Tiny Swords food / hunting / meat gathering animation available in the imported pack.

Do not assume the exact clip name.

Inspect the source asset and record the actual filename below before implementation.

Gameplay relationship:

Sheep → Food

Loop while gathering:

YES

---

## BUILD

Gameplay condition:

Pawn is constructing a building.

Required visual:

Tiny Swords Pawn construction animation.

Loop:

YES while construction is active.

Gameplay effects are controlled by BuildingSystem, not by animation frames.

---

## REPAIR

Gameplay condition:

Pawn is repairing a damaged friendly building.

Required visual:

Tiny Swords Pawn repair animation.

Loop:

YES while repair is active.

Gameplay effects are controlled by RepairSystem.

---

# 3. Warrior

Gameplay entity:

Warrior

Gameplay states:

IDLE
MOVE
CHASE
ATTACK
GUARDING
HIT
DEAD

Animation mapping:

IDLE → Warrior Idle

MOVE → Warrior Run

CHASE → Warrior Run

ATTACK → Warrior Attack1, once per authorized attack

GUARDING → Warrior Guard, looping while held

HIT → Warrior Hit animation if available

DEAD → Warrior Death animation if available

Never use Pawn animations for Warrior.

---

# 4. Archer

Gameplay entity:

Archer

Gameplay states:

IDLE
MOVE
CHASE
ATTACK
HIT
DEAD

Animation mapping:

IDLE → Archer Idle

MOVE → Archer Run

CHASE → Archer Run

ATTACK → Archer Bow Attack

HIT → Archer Hit animation if available

DEAD → Archer Death animation if available

Projectile creation must be controlled by CombatSystem.

The animation must not independently apply damage.

---

# 5. Lancer

Gameplay entity:

Lancer

Gameplay states:

IDLE
MOVE
CHASE
ATTACK
BLOCK
HIT
DEAD

Animation mapping:

IDLE → Lancer Idle

MOVE → Lancer Run

CHASE → Lancer Run

ATTACK → Lancer Attack

BLOCK → Lancer defensive/block animation

HIT → Lancer Hit animation if available

DEAD → Lancer Death animation if available

---

# 6. Resources

## TREE

Resource:

WOOD

Visual stages should use available Tiny Swords tree assets.

Possible lifecycle:

TREE_FULL
TREE_DAMAGED / gathering feedback
TREE_STUMP
DEPLETED

When wood reaches zero:

replace Tree visual with appropriate stump visual if available.

Do not use a random stump.

Use the stump variant associated with the tree asset when identifiable.

---

## GOLD_ROCK

Resource:

GOLD

Tiny Swords provides multiple Gold Rock sizes.

Different rock sprites may represent remaining resource amount.

Example logical stages:

GOLD_FULL
GOLD_HIGH
GOLD_MEDIUM
GOLD_LOW
GOLD_DEPLETED

The exact sprite mapping must be determined from the imported asset.

---

## SHEEP

Resource:

FOOD

Possible states:

IDLE
MOVING
RESOURCE_AVAILABLE
DEPLETED

If Tiny Swords provides dedicated sheep/meat states, use those assets.

Inspect the source files before deciding the final state mapping.

---

# 7. Buildings

Every building must have its own asset mapping.

Initial buildings:

CASTLE
HOUSE
BARRACKS
TOWER

Possible visual states:

CONSTRUCTION
NORMAL
DAMAGED
HEAVILY_DAMAGED
DESTROYED

Use available Tiny Swords building and visual effect assets where appropriate.

Fire effects may represent damaged buildings.

Explosion effects may represent building destruction.

Gameplay HP remains controlled by BuildingSystem.

---

# 8. Faction Colors

Tiny Swords provides faction color variants.

Current game mapping:

BLUE → Player / Faction 1

RED → Enemy / Faction 2

Future supported colors may include:

PURPLE
YELLOW
BLACK

Do not manually recolor sprites if an appropriate official faction variant already exists.

---

# 9. Exact Asset Registry

This section MUST be completed by inspecting the actual imported Tiny Swords files.

Do not invent paths.

Format:

Entity | Gameplay State | Exact Asset Path | Clip | Loop

Example:

Pawn | Idle | [inspect project] | Pawn_Idle | YES

Pawn | Move | [inspect project] | Pawn_Move | YES

Pawn | Gather Wood | [inspect project] | [actual clip] | YES

Pawn | Gather Gold | [inspect project] | [actual clip] | YES

Pawn | Gather Food | [inspect project] | [actual clip] | YES

Pawn | Build | [inspect project] | [actual clip] | YES

Pawn | Repair | [inspect project] | [actual clip] | YES

Warrior | Idle visual (Phase 3 test unit) | Assets/Units/Blue Units/Warrior/Warrior_Idle.png | Static sprite `Warrior_Idle_0`; no AnimationClip | NO

The current Blue/Red Warrior, Archer and Lancer mappings are recorded in the
verified unit animation registry in section 10 below.

Phase 3 verification note:

* `Warrior_Idle.png` is a 1536x192 Blue Warrior idle sheet with 8 imported sprites.
* Phase 3 uses only the inspected `Warrior_Idle_0` sprite as a static visual.
* Phase 4A Red Warrior test unit uses the inspected `Assets/Units/Red Units/Warrior/Warrior_Idle.png` sprite `Warrior_Idle_0` as a static visual. The imported sheet has eight idle sprites; no combat animation is mapped.

## Phase 4B1 verified static combat visuals

| Unit / effect | Blue source | Red source | Imported sprite used |
| --- | --- | --- | --- |
| Archer idle | `Assets/Units/Blue Units/Archer/Archer_Idle.png` | `Assets/Units/Red Units/Archer/Archer_Idle.png` | `Archer_Idle_0` |
| Lancer idle | `Assets/Units/Blue Units/Lancer/Lancer_Idle.png` | `Assets/Units/Red Units/Lancer/Lancer_Idle.png` | `Lancer_Idle_0` |
| Archer projectile | `Assets/Units/Blue Units/Archer/Arrow.png` | `Assets/Units/Red Units/Archer/Arrow.png` | `Arrow_0` |

These files were visually inspected in the imported project. Phase 4B1 uses only these static sprites; no Run, Shoot, Attack or Defence frame range is mapped or used for gameplay timing.
* No Idle or Run AnimationClip is configured, and no moving frame range is inferred.

---

# 10. Verified Phase 4B1 Unit Animation Registry

All paths in this table exist in both `Assets/Units/Blue Units/` and
`Assets/Units/Red Units/`, followed by the relative path shown. Blue and Red
have the same frame geometry. Frame numbers are left to right, starting at 0.
Frames are shown at 10 FPS; this is the project's playback setting, not timing
metadata embedded in the PNG. Death sheets are absent for these three units.

| Unit | State | Relative asset path | Frames | Frame cell | Loop |
| --- | --- | --- | ---: | --- | --- |
| Warrior | Idle | `Warrior/Warrior_Idle.png` | 8 | 192x192 | Yes |
| Warrior | Moving, Chasing | `Warrior/Warrior_Run.png` | 6 | 192x192 | Yes |
| Warrior | Attack event | `Warrior/Warrior_Attack1.png` | 4 | 192x192 | No; once per authorized attack |
| Warrior | Guarding | `Warrior/Warrior_Guard.png` | 6 | 192x192 | Yes |
| Archer | Idle | `Archer/Archer_Idle.png` | 6 | 192x192 | Yes |
| Archer | Moving, Chasing | `Archer/Archer_Run.png` | 4 | 192x192 | Yes |
| Archer | Attack event | `Archer/Archer_Shoot.png` | 8 | 192x192 | No; once per authorized shot |
| Lancer | Idle | `Lancer/Lancer_Idle.png` | 12 | 320x320 | Yes |
| Lancer | Moving, Chasing | `Lancer/Lancer_Run.png` | 6 | 320x320 | Yes |
| Lancer | Attack down | `Lancer/Lancer_Down_Attack.png` | 3 | 320x320 | No |
| Lancer | Attack down-right | `Lancer/Lancer_DownRight_Attack.png` | 3 | 320x320 | No |
| Lancer | Attack right | `Lancer/Lancer_Right_Attack.png` | 3 | 320x320 | No |
| Lancer | Attack up-right | `Lancer/Lancer_UpRight_Attack.png` | 3 | 320x320 | No |
| Lancer | Attack up | `Lancer/Lancer_Up_Attack.png` | 3 | 320x320 | No |

The original automatic slicing split VFX or bodies in Warrior_Attack1,
Archer_Shoot, Lancer_Idle and Lancer_Right_Attack for both factions. Those eight
importers now use full cells with a center pivot. All other used sheets retain
their existing imported sprite rectangles; presentation aligns their pixels to
the same per-unit reference anchor. `Warrior_Attack2.png` is a verified four-cell
alternative but is not used yet; its current automatic imported fragments must
not be played as frames. No Lancer Defence gameplay is mapped.
`Lancer_Idle.png` is 3840 pixels wide, so its Blue and Red importer maximum
texture size is 4096; a 2048 limit silently reduced the imported frames.

## Verified unit UI assets

`Assets/UI Elements/UI Elements/Icons/Icon_06.png` depicts a shield and is used
for the Guard button. Exact portrait-to-unit mappings in `Human Avatars` remain
unconfirmed; use verified unit Idle sprites for selection portraits for now.

# 11. Important Rule For Codex

Before creating animation code for an entity:

1. Inspect its actual Tiny Swords asset directory.
2. Identify available sprite sheets and animations.
3. Determine what gameplay action each animation represents.
4. Update the Exact Asset Registry in this file.
5. Only then create Animation Clips / Animator configuration.
6. Never invent an animation that does not exist.
7. Never substitute another animation without documenting the substitution.
8. If an expected animation does not exist, report it instead of silently using an unrelated one.
