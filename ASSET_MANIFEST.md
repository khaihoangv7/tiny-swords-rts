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
ARCHERY
MONASTERY

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

### Phase 5B1 verified building mapping

The imported Blue and Red building images below each contain one static sliced
sprite. Source PNG size and sliced rect size are in pixels. The sprite importer
uses 100 PPU and centered alignment; the `pivot: {x: 0, y: 0}` text in the
slice metadata is ignored when alignment is Center. Logical footprints are
explicit `BuildingData` values and are not inferred from these images.

| Gameplay type | Blue PNG | Red PNG | Source | Slice |
| --- | --- | --- | --- | --- |
| House | `Assets/Buildings/Blue Buildings/House1.png` | `Assets/Buildings/Red Buildings/House1.png` | 128x192 | 114x159 |
| Barracks | `Assets/Buildings/Blue Buildings/Barracks.png` | `Assets/Buildings/Red Buildings/Barracks.png` | 192x256 | 186x189 |
| Tower | `Assets/Buildings/Blue Buildings/Tower.png` | `Assets/Buildings/Red Buildings/Tower.png` | 128x256 | 122x186 |
| Castle | `Assets/Buildings/Blue Buildings/Castle.png` | `Assets/Buildings/Red Buildings/Castle.png` | 320x256 | 314x210 |

The Map currently uses the Blue Castle static sprite as its existing resource
drop-off. Phase 5B1 constructs Blue House, Barracks and Tower with these normal
sprites. Their under-construction representation is the same sprite at partial
opacity plus a separate progress bar. No dedicated construction, damaged or
destroyed building sprite was found in the building folders. The three Fire
particle sheets are effects, not verified building damage states, and are unused.
Black, Purple and Yellow folders also contain one-frame House1, Barracks, Tower
and Castle variants at the same source dimensions; they are not mapped to a
current gameplay faction and are unused.

Blue Pawn construction tools are verified from the actual sheets:

| State | PNG | Source | Frames | Slice alignment |
| --- | --- | --- | --- | --- |
| Build / repair impact | `Assets/Units/Blue Units/Pawn/Pawn_Interact Hammer.png` | 576x192 | 3 | Center, 100 PPU |
| Hammer recovery / idle | `Assets/Units/Blue Units/Pawn/Pawn_Idle Hammer.png` | 1536x192 | 8 | Center, 100 PPU |
| Walking with hammer | `Assets/Units/Blue Units/Pawn/Pawn_Run Hammer.png` | 1152x192 | 6 logical frames | Center, 100 PPU |

The animation frame order follows the existing import metadata (`_0`, `_1`,
etc.). The imported Run Hammer `_5` was a separate 27x30 fragment of logical
frame `_4` within the same 192-pixel column. Its bounds were merged into `_4`;
the animation uses `_0`, `_1`, `_2`, `_3`, `_4`, `_6` in that order. The other
Hammer slices were preserved. Construction and repair timing are controlled by
gameplay code.

### Phase 5C1 building and resource additions

The following Blue/Red PNG pairs each import as one centered, static sprite at
100 PPU. Normal sprites are used on completion; an unfinished building uses
partial opacity and the separate construction progress bar. No dedicated
construction or damaged variant was found for these buildings.

| Type / visual variant | Blue source | Red source | Source pixels | Sliced rect |
| --- | --- | --- | --- | --- |
| House B | `Assets/Buildings/Blue Buildings/House2.png` | `Assets/Buildings/Red Buildings/House2.png` | 128x192 | 128x157 |
| House C | `Assets/Buildings/Blue Buildings/House3.png` | `Assets/Buildings/Red Buildings/House3.png` | 128x192 | 124x137 |
| Archery | `Assets/Buildings/Blue Buildings/Archery.png` | `Assets/Buildings/Red Buildings/Archery.png` | 192x256 | 185x181 |
| Monastery | `Assets/Buildings/Blue Buildings/Monastery.png` | `Assets/Buildings/Red Buildings/Monastery.png` | 192x320 | 162x267 |

House A remains `House1.png` from the Phase 5B1 mapping. A/B/C use one House
gameplay data asset and one 2x2 logical footprint; the visual choice does not
alter population. The build buttons use the verified building sprites as icons;
no dedicated building icon mapping was confirmed.

The Blue and Red Monk folders contain `Idle.png` (1152x192, six imported
frames) and `Run.png` (768x192, four imported frames). `Heal.png` and
`Heal_Effect.png` are each 2112x192, but their current importers split the
art into 37 and 99 fragments respectively. They are **not** a verified playable
healing animation mapping. Monk remains unspawnable until its gameplay data
and healing animation are specified and validated.

The Map uses `Tree1_0` from `Assets/Terrain/Resources/Wood/Trees/Tree1.png`.
Its visible trunk base matches `Stump 1_0` from
`Assets/Terrain/Resources/Wood/Trees/Stump 1.png` (192x256 source, one 50x42
slice, centered, 100 PPU). On Wood depletion the node swaps to this stump;
the node stays depleted and releases its navigation occupancy. `Stump 2/3/4`
are present for Tree2/3/4 but are not used by the Map test scene.

The Map Sheep is `Sheep_Idle_0` from
`Assets/Terrain/Resources/Meat/Sheep/Sheep_Idle.png` (768x128 sheet, six
imported sprites). A successful Food gather impact briefly tints the current
Sheep visual red for 0.14 seconds. This is presentation only; the same
`ResourceNode.RemainingAmount` controls depletion and the final Sheep visual
is hidden after the last flash.

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
Frames are shown at 10 FPS except Archer Shoot, which plays at 6.667 FPS to
align the drawn-bow pose with the configured 0.7-second projectile release.
These are project playback settings, not timing metadata embedded in the PNG.
Death sheets are absent for these three units.

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

Archer Shoot uses eight configured frames at approximately 6.667 FPS (1.2
seconds total). At the 0.7-second gameplay release, frame index 4 is still
showing the drawn arrow; the next frame begins at 0.75 seconds
and shows its release. `CombatController` controls the projectile release;
animation frames and events never authorize it. Idle and Run remain at 10 FPS.

The Blue and Red `Archer_Shoot.png` sheets show the drawn arrow pointing right
in the fourth and fifth 192x192 cells. Its tip is approximately at cell pixel
(130, 110), measured from the cell's lower-left corner. Relative to the Archer
visual anchor (93, 100) and 100 pixels per world unit, the projectile release
point is approximately (+0.37, +0.10) world units. `CombatController` uses this
visual offset (rounded to +0.4, +0.1) and mirrors X when firing left. Blue and
Red `Arrow.png` depict a right-pointing arrow, so the projectile rotates along
its flight direction. The arc and release offset are presentation settings;
impact still determines gameplay damage.

## Phase 5C2 defensive presentation

Tower and Castle generic defensive fire reuses the already verified faction
Archer `Arrow_0` projectile mapping and the existing arc presentation. Arrows
launch from explicit BuildingData offsets rather than from hidden occupants.
Mounted Archers reuse the complete existing Archer Idle/Shoot and projectile
pipeline; no alternate mounted animation sheet was found or inferred.

Tower uses one explicit rooftop anchor. Castle uses three explicit rooftop
anchors. These positions are gameplay configuration and are not calculated from
transparent sprite bounds. No verified Tower or Castle destroyed/ruin sprite
was found in the building folders, so Phase 5C2 hides the destroyed host visual
after safe ejection instead of guessing an asset mapping.

## Verified unit UI assets

`Assets/UI Elements/UI Elements/Icons/Icon_06.png` depicts a shield and is used
for the Guard button. Exact portrait-to-unit mappings in `Human Avatars` remain
unconfirmed; use verified unit Idle sprites for selection portraits for now.

## Verified Tiny Swords cursors

These source PNGs are 64x64 RGBA images with transparent margins. Hotspots use
pixel coordinates measured from the upper-left corner of the full texture,
because `Cursor.SetCursor` receives the texture rather than its cropped sprite.

| Visual | Source PNG | Visible bounds (xMin, yMin, xMax, yMax) | Hotspot |
| --- | --- | --- | --- |
| Default arrow | `Assets/UI Elements/UI Elements/Cursors/Cursor_01.png` | (21, 16, 45, 48) | (22, 17), arrow tip |
| Friendly hand | `Assets/UI Elements/UI Elements/Cursors/Cursor_02.png` | (19, 16, 48, 50) | (24, 18), pointing finger |
| Forbidden | `Assets/UI Elements/UI Elements/Cursors/Cursor_03.png` | (15, 12, 49, 50) | (32, 31), symbol center |

The three cursor textures retain Sprite import and existing slicing, with
Read/Write enabled for `Cursor.SetCursor`, Point filtering, no compression,
no mipmaps and Clamp wrapping. The hand represents friendly hover and the
default cursor state while controllable units are selected. The forbidden
symbol represents an unavailable Move destination. UI and enemy hover use
the default arrow.

# 11. Phase 5A inspected economy visuals

These PNGs were inspected directly in the project. The Map test setup uses the
listed imported sprites; the Idle/Run and resource slicing listed here is unchanged.
Gameplay gathering uses `PawnGatherer` timers, independent of these visuals.

| Purpose | Tiny Swords source | Imported sprites used |
| --- | --- | --- |
| Blue Pawn Idle | `Assets/Units/Blue Units/Pawn/Pawn_Idle.png` | `Pawn_Idle_0` through `_7`, eight 192x192 cells |
| Blue Pawn Run | `Assets/Units/Blue Units/Pawn/Pawn_Run.png` | `Pawn_Run_0` through `_5`, six 192x192 cells |
| Sheep food node | `Assets/Terrain/Resources/Meat/Sheep/Sheep_Idle.png` | Static `Sheep_Idle_0` |
| Tree wood node | `Assets/Terrain/Resources/Wood/Trees/Tree1.png` | Static `Tree1_0` |
| Gold rock node | `Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone 1.png` | Static `Gold Stone 1_0` |
| Blue Castle drop-off | `Assets/Buildings/Blue Buildings/Castle.png` | Static `Castle_0` |

## Phase 5A verified Pawn work and combat mappings

The following sheets were inspected in both `Assets/Units/Blue Units/Pawn/`
and `Assets/Units/Red Units/Pawn/`. Every source frame occupies a 192x192
cell. Unity's previous automatic slicing split loose Axe/Pickaxe/Knife pixels
into extra sprite fragments (7/8/7 sprites); those three sheets now preserve
the complete 6/6/4 source cells as named sprite frames. PPU remains 100.

| Gameplay visual | Sheet in each faction Pawn directory | Frames | Playback |
| --- | --- | --- | --- |
| Idle | `Pawn_Idle.png` | `_0`–`_7` (8) | loop, about 10 fps |
| Move | `Pawn_Run.png` | `_0`–`_5` (6) | loop, about 10 fps |
| Wood work | `Pawn_Interact Axe.png` | `_0`–`_5` (6) | 12 fps, 0.5 s once per 1.5 s gather cycle |
| Gold work | `Pawn_Interact Pickaxe.png` | `_0`–`_5` (6) | 12 fps, 0.5 s once per 1.5 s gather cycle |
| Food work and combat | `Pawn_Interact Knife.png` | `_0`–`_3` (4) | work: 8 fps, 0.5 s once per 1.5 s cycle; combat: 10 fps |
| Wood gathering recovery | `Pawn_Idle Axe.png` | `_0`–`_7` (8) | tool Idle at about 10 fps until next work cycle |
| Gold gathering recovery | `Pawn_Idle Pickaxe.png` | `_0`–`_7` (8) | tool Idle at about 10 fps until next work cycle |
| Food gathering recovery | `Pawn_Idle Knife.png` | `_0`–`_7` (8) | tool Idle at about 10 fps until next work cycle |
| Carry Wood moving / still | `Pawn_Run Wood.png` / `Pawn_Idle Wood.png` | 6 / 8 | loop, about 10 fps |
| Carry Gold moving / still | `Pawn_Run Gold.png` / `Pawn_Idle Gold.png` | 6 / 8 | loop, about 10 fps |
| Carry Food moving / still | `Pawn_Run Meat.png` / `Pawn_Idle Meat.png` | 6 / 8 | loop, about 10 fps |

The Map test scene uses the Blue set in `Assets/UnitAnimations/BluePawn.asset`.
The three Blue tool Idle sheets were inspected as eight-frame 192x192-cell
animations, and their importers now use Point filtering and no compression.
The corresponding Red tool Idle sheets exist but there is no Red Pawn test unit
or Pawn prefab in this phase. Work animation and combat animation observe gameplay
events; they do not determine resource gain or damage.

Verified resource popup icons: Food uses
`Assets/Terrain/Resources/Meat/Meat Resource/Meat Resource.png` (`Meat Resource_0`),
Wood uses `Assets/Terrain/Resources/Wood/Wood Resource/Wood Resource.png`
(`Wood Resource_0`), and Gold uses
`Assets/Terrain/Resources/Gold/Gold Resource/Gold_Resource.png`
(`Gold_Resource_0`).

The health bar uses `Assets/UI Elements/UI Elements/Bars/SmallBar_Base.png`
(`SmallBar_Base_0`, `_1`, `_2` for left, middle, right) and
`Assets/UI Elements/UI Elements/Bars/SmallBar_Fill.png`
(`SmallBar_Fill_0`, red stripe). The source base has transparent slice padding;
at runtime its 16 + 64 + 16 opaque columns are assembled into one static
96x21 frame sprite. The source fill is only 64x3 opaque pixels inside a
64x64 transparent sprite, so its red color is used for one solid 74x6 fill.
The usable rectangle starts at frame pixel (11,6), measured from the lower
left. HP crops whole fill pixels from the right; the left edge and six-pixel
height stay fixed. Both source textures are readable for this assembly and
retain Point filtering, no compression, no mipmaps, and 100 PPU. All newly used work,
carry, icon, and bar sheets
use Point filtering, no compression, and no mipmaps. The Castle remains only a
Phase 5A drop-off visual; construction and production are outside this phase.

# 12. Phase 5C2.1 verified presentation assets

The mounted Archer keeps the existing unit pivot. Its root is placed at an
explicit BuildingData rooftop anchor so the visual foot point, approximately
0.48 world units below the unit root, meets the wooden platform. Tower first
changed from `(0, 0.45)` to `(0, 0.93)` and the targeted Play Mode correction
lowers only Tower to `(0, 0.83)`. Castle remains changed from
`(-0.72, 0.35), (0, 0.35), (0.72, 0.35)` to
`(-0.95, 0.83), (0, 0.83), (0.95, 0.83)` for both current faction sprites.
These remain visual offsets and do not change Archer gameplay position outside
mounted state.

The source `Assets/Terrain/Resources/Meat/Sheep/Sheep.aseprite` contains 22
128x128 frames, each with a verified 100 ms duration. Its forward-loop tags and
exported PNG mappings are:

| Sheep state | Exact path | Frames | Playback |
| --- | --- | ---: | --- |
| Idle | `Assets/Terrain/Resources/Meat/Sheep/Sheep_Idle.png` | 6 (`_0`-`_5`) | 10 FPS loop while stationary |
| Grass | `Assets/Terrain/Resources/Meat/Sheep/Sheep_Grass.png` | 12 (`_0`-`_11`) | 10 FPS ambient loop while stationary |
| Move | `Assets/Terrain/Resources/Meat/Sheep/Sheep_Move.png` | 4 (`_0`-`_3`) | 10 FPS loop only while moving |

`Assets/Terrain/Resources/Wood/Trees/Trees.aseprite` verifies four forward-loop
Tree tags of eight 100 ms frames and four single-frame matching Stump tags.

| Variant | Tree path / imported frames | Depleted path / sprite |
| --- | --- | --- |
| 1 | `Assets/Terrain/Resources/Wood/Trees/Tree1.png`, `Tree1_0`-`_7` | `Assets/Terrain/Resources/Wood/Trees/Stump 1.png`, `Stump 1_0` |
| 2 | `Assets/Terrain/Resources/Wood/Trees/Tree2.png`, `Tree2_0`-`_7` | `Assets/Terrain/Resources/Wood/Trees/Stump 2.png`, `Stump 2_0` |
| 3 | `Assets/Terrain/Resources/Wood/Trees/Tree3.png`, `Tree3_0`-`_7` | `Assets/Terrain/Resources/Wood/Trees/Stump 3.png`, `Stump 3_0` |
| 4 | `Assets/Terrain/Resources/Wood/Trees/Tree4.png`, `Tree4_0`-`_7` | `Assets/Terrain/Resources/Wood/Trees/Stump 4.png`, `Stump 4_0` |

`Assets/Terrain/Resources/Gold/Gold Stones/Gold Stones.aseprite` verifies six
Stone groups. Every `Gold Stone N.png` has one Still sprite. Every matching
`Gold Stone N_Highlight.png` has six frames: five 100 ms frames followed by a
500 ms final frame. Highlight plays once per successful mining impact and also
after each full 10-second ambient wait measured from the previous playback's
completion. Active playback is never stacked. It then returns to that Stone's
Still sprite and resets the wait.

| Variant | Still path | Highlight path |
| --- | --- | --- |
| 1 | `Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone 1.png` | `Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone 1_Highlight.png` |
| 2 | `Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone 2.png` | `Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone 2_Highlight.png` |
| 3 | `Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone 3.png` | `Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone 3_Highlight.png` |
| 4 | `Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone 4.png` | `Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone 4_Highlight.png` |
| 5 | `Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone 5.png` | `Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone 5_Highlight.png` |
| 6 | `Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone 6.png` | `Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone 6_Highlight.png` |

The new death source is `Assets/Particle FX/Dead.aseprite`, with the matching
`Assets/Particle FX/Dead.png` export at 896x256. The Aseprite file has two
forward tags: `Dead` frames 0-6 and `Buried` frames 7-13. Frames are 128x128.
The exact source durations are 100 ms for frames 0-5, 500 ms for frame 6, and
100 ms for frames 7-13. Phase 6C.1 plays all fourteen frames continuously as a
1.8-second faction-neutral, non-directional unit death effect. The VFX has no
collider, damage, selection or navigation behavior, and no corpse remains after
frame 13.

The preferred runtime source is the fourteen serialized `Dead.png` Sprite
subassets in `Assets/Resources/Presentation/Phase5C21Presentation.asset`. A
previous catalog generation pass serialized `deadFrames` as an empty array,
causing the generic death spawn method to return without creating a visible
object. `UnitDeathVfx` now validates those references and falls back to
`Assets/Resources/Presentation/DeadRuntime.bytes`, an exact copy of the verified
896x256 PNG. It creates the seven top-row Dead frames followed by the seven
bottom-row Buried frames at 100 PPU once and caches them. Editor setup keeps that
runtime atlas synchronized with the source PNG. The independent VFX renders at
world sorting order 18 and destroys itself after the one-shot.

All PNGs introduced into current gameplay by this section retain their verified
slicing and 100 PPU, with Point filtering, no compression, no mipmaps, Clamp
wrapping and a Max Size large enough for the source dimensions.

## Phase 6B.1 Rally marker

Phase 6C.1 initially used `Icon_07_0`. Targeted visual review showed that
`Icon_07` is a green resource/food-shaped icon rather than a battlefield flag.
The requested correction uses `Assets/UI Elements/UI Elements/Icons/Icon_09.png`,
imported as `Icon_09_0`. It visibly contains two crossed red weapons. The PNG
source is 64x64; its single tight sprite rectangle is 53x57 pixels at `(5,3)`,
centered alignment and 100 PPU. Import is Point filtered, mipmaps off,
uncompressed and Clamp wrapped. This is a deliberate Rally marker choice, not
a claim that the source artwork is a literal flag. `Icon_08` was also inspected
and resembles an orange banner/flag, but remains unused. The marker is
presentation only and has no collider, raycast, navigation or Fog role.

## Phase 6C.1 shadow audit

Pawn, Warrior, Archer, Lancer, current buildings, Tree, Gold and Sheep have
their shadows baked into their main Tiny Swords sprites. They have no separate
Shadow SpriteRenderer that can be darkened independently, so Phase 6C.1 does
not tint or rewrite those main sprites. The only separate gameplay shadow asset
is `Assets/Terrain/Tileset/Shadow.png`, a 192x192 terrain/elevation shadow used
by `Shadow_L1`. Its one nontransparent source color remains dark navy RGB
`(22,28,46)`; alpha first changed from `80/255` to `128/255`, then the targeted
readability correction raises it to `153/255` (60%). `Shadow_L1` tint changes
from white `(1,1,1,1)` to dark neutral `(0.28,0.32,0.42,1)`. Its existing
Tilemap offset, cells and sorting are unchanged.

## Phase 6B.2 verified building damage and Life Bar assets

`Assets/Particle FX/Particle FX.aseprite` is the authoritative animation
source. Its relevant forward tags all use 100 ms per frame (10 FPS):

| Gameplay presentation | Exact exported PNG | Source size | Frame cell | Frames | Runtime |
| --- | --- | ---: | ---: | ---: | --- |
| Fire 1 | `Assets/Particle FX/Fire_01.png` | 512x64 | 64x64 | 8 (`_0`-`_7`) | loop |
| Fire 2 | `Assets/Particle FX/Fire_02.png` | 640x64 | 64x64 | 10 (`_0`-`_9`) | loop |
| Fire 3 | `Assets/Particle FX/Fire_03.png` | 768x64 | 64x64 | 12 (`_0`-`_11`) | loop |
| Explosion 1 | `Assets/Particle FX/Explosion_01.png` | 1536x192 | 192x192 | 8 (`_0`-`_7`) | one-shot |
| Explosion 2 | `Assets/Particle FX/Explosion_02.png` | 1920x192 | 192x192 | 10 (`_0`-`_9`) | one-shot |

These PNGs previously used automatic tight slicing, which split one logical
effect frame into separate particle fragments. Phase 6B.2 slices them by the
verified full frame cells above, with centered pivots, 100 PPU, Point filtering,
no compression, no mipmaps and Clamp wrapping. The three Fire effects run at
separate building-local anchors while a completed building is below 70 percent
HP. Both Explosion effects play once with a 0.06-second visual offset after
building gameplay death. These effects have no collider or gameplay authority.

The large building Life Bar maps to:

| Part | Exact path | Source / slice |
| --- | --- | --- |
| Static frame pieces | `Assets/UI Elements/UI Elements/Bars/BigBar_Base.png` | 320x64 source; 26x53 left, 66x53 middle, 26x53 right |
| Continuous red fill | `Assets/UI Elements/UI Elements/Bars/BigBar_Fill.png` | 64x64 source; one 64x26 fill slice |

At runtime the frame pieces are assembled once into a static 114x53 frame.
The measured inner container is 90x26 at pixel `(12,13)`. The 64x26 source fill
is treated as a repeatable pixel pattern and expanded to the full 90-pixel
runtime width; whole columns are cropped from its right edge as HP falls, so the
left edge and height remain fixed. Both bar textures are readable at runtime and retain 100
PPU, Point filtering, no compression, no mipmaps and Clamp wrapping. Unit HP
continues to use the separate verified SmallBar mapping.

## Phase 6C verified Match Result UI assets

The inspected Ribbon/Banner candidates are:

| Asset | Source size | Imported slicing | Assessment |
| --- | ---: | ---: | --- |
| `Assets/UI Elements/UI Elements/Ribbons/BigRibbons.png` | 448x640 | 15 sprites: five color rows, three pieces per row | Selected; wide title area and compact source |
| `Assets/UI Elements/UI Elements/Ribbons/SmallRibbons.png` | 320x640 | 30 sprites | Too compact for the centered result title |
| `Assets/UI Elements/UI Banners from the store page/Ribbons/Ribbon_Blue.png` | 960x576 | 14 sprites | Suitable alternatives, but includes several larger banner forms not needed by this result panel |
| `Assets/UI Elements/UI Banners from the store page/Ribbons/Ribbon_Red.png` | 960x576 | 14 sprites | Same alternate layout in Red |
| Matching store-page Purple, Yellow and Black sheets | 960x576 each | 14 sprites each | Inspected color variants; unused |
| `Assets/UI Elements/UI Elements/Banners/Banner.png` | 448x448 | 9 sprites | General panel/banner pieces; SpecialPaper better frames the result content |

Runtime result mapping:

| Use | Exact mapping |
| --- | --- |
| Victory title | `BigRibbons_0`, `_1`, `_2` from `BigRibbons.png` (Blue row) |
| Defeat title | `BigRibbons_3`, `_4`, `_5` from `BigRibbons.png` (Red row) |
| Result panel | `Assets/UI Elements/UI Elements/Papers/SpecialPaper.png`, all nine `SpecialPaper_0`-`_8` pieces |
| Victory Restart normal/pressed | `SmallBlueSquareButton_Regular_0` / `SmallBlueSquareButton_Pressed_0` |
| Defeat Restart normal/pressed | `SmallRedSquareButton_Regular_0` / `SmallRedSquareButton_Pressed_0` |

The selected textures retain their verified slicing. Phase 6C sets only these
used UI textures to Sprite, Point filtering, no mipmaps, no compression, Clamp
wrapping and a Max Size large enough for their source. No external UI or audio
asset is introduced; the project currently has no gameplay audio system or
imported font asset.

# 13. Important Rule For Codex

Before creating animation code for an entity:

1. Inspect its actual Tiny Swords asset directory.
2. Identify available sprite sheets and animations.
3. Determine what gameplay action each animation represents.
4. Update the Exact Asset Registry in this file.
5. Only then create Animation Clips / Animator configuration.
6. Never invent an animation that does not exist.
7. Never substitute another animation without documenting the substitution.
8. If an expected animation does not exist, report it instead of silently using an unrelated one.
