# Tiny Kingdom RL — Asset Manifest

## Straight Lane second map (October 2026)

Map two uses verified Tiny Swords `Tilemap_color1` ground/shore tiles and
animated `Water Foam.png` to form two base clearings connected by a
single widened horizontal corridor and round central mine island.
`Tilemap_color4` marks the five-cell road and circular mine clearing.
Color3 supplies each height-1 base and outer height-2 Castle court, with one
finished cliff-foot row r5c5/r5c6/r5c7 per tier. Each gate paints TWO adjacent
copies of the same native ramp pair: r4c3/r5c3 for Blue (descending right),
r4c0/r5c0 for Red (descending left). The Color3 source sheet was inspected
again; no opposing/mirrored ramp pieces are joined within one gate.
The latest user-confirmed placement is two front/back native copies touching
with no grass cell between them, now one cell closer: rows 20/21 and 21/22
share one X and overlap at row 21. Blue gates use x=18/10; Red x=41/49.
The rear native pair remains on ElevatedGround_L1/L2; the front native pair
uses StairFront_L1/L2 in terrain background orders above the rear pair and below
depth-sorted units, so neither source pair is replaced or cropped and units remain
visible while crossing. A native Color3 interior tile backs the
transparent ramp corners so contrasting Color1 grass cannot open a visual gap.
No rotation, scale change or flipping is applied to either complete copy.
The lower tiers expand to 15x16; upper courts remain 7x5 and are
centered on the existing Castle positions. The October sketch removes the outer
H2 pine at (3.5,21.5) and its Red counterpart, adds four northern-rim pines per
side and six extra pines in the marked Blue flank/court corners. Existing other
pines outside the northern bands keep their positions and variants. The latest
red-band revision adds six H0 pines per side and brings the existing y=32 row
inward to y=31, leaving a clear harvest aisle behind it. There are 93 Blue and
87 Red Wood nodes (28/22 on the tiers); all 232 resource nodes reuse the catalog.
The source Color3 sheet was re-inspected against the user's single-ramp image;
the upper r4 and lower r5 pieces stay together in each complete copy.
Native sprite orientation, 64 PPU, size and tile pivots remain unchanged.
The user's two pine references were matched by inspecting Tree1.png (dark,
broad pine) and Tree2.png (bright, narrow pine). Map two uses catalog indices
0/1 ONLY, preserving verified eight-frame loops and Stump 1/2 depletion.
Tree3/4 are excluded from this map. Dense forests are real Wood resources;
20 Sheep and six Gold Rocks per side reuse existing mappings/capacities.
All Sheep spawn on the first tier (H1); no Sheep remain on the surrounding H0 ground.
Sheep positions are hand-scattered into different, non-mirrored H1 flocks.
The previous red-marked Sheep were relocated; the latest four pink-marked Sheep
are deleted at (7.25,16.55), (11.3,16.8), (54.3,16.7) and (50.7,16.25).
All other Sheep retain their positions/names: 40 remain, 20 per side.
Each side's six Gold Rocks form one compact, irregular H1 cluster using the
inspected Gold Stone 5/6 (large), 3/4 (medium), 2 (small) and 1 (tiny) sprites,
catalog indices 4/5/2/3/1/0. Existing matched Highlight frames, capacities,
1.4 scale (Stone 1-5) and 1.55 scale (Stone 6) remain authoritative.
Existing Blue/Red Castle, Pawn and Sheep mappings are reused for gameplay.
Static `Bushe3.png` frames the base corners; four additional scattered bushes
use the first imported frame of Bushe1/2/3/4.png (one of each), at order 8.
These bushes are static decoration only, not Wood resources or blockers.
Six decorative Ducks reuse the verified three 32x32 `Rubber duck.png` frames
at 64 PPU and the existing 10 FPS AmbientSpriteLoop. They float only on water,
at order 3 and scale 1.6, with no collider, resource value, movement or vision.
Three yellow-marked patches add 24 static bushes, mixing the first imported
frames of Bushe1/2/3/4 at 100 PPU with varied 0.8-1.25 presentation scales.
The two ground rock patches use six static Rock1/2/3/4 sprites at their existing
100 PPU, varying scale 0.8-1.8. Two water patches add four Water Rocks_02/04
objects, retaining sixteen native 64x64 frames at 64 PPU, 10 FPS and scale 0.8/1.3.
Existing water rocks and all six Ducks are retained. None occupies navigation.
Ten overhead clouds reuse whole native Clouds_01/02/03/04/05/08 PNGs (576x256,
one centered Sprite at 64 PPU). They are not frame strips: AmbientCloudDrift
translates the static illustration on slow 52-79 second cycles, with small
vertical movement. Large illustrations use scale 1.1; small 03/04/08 use 1.8.
Alpha 0.62-0.72 and order 19 keep the map readable. Their full bounds and travel
stay inside the map, away from Castles, stairs, Gold clusters and central mine.
No collider, resource, vision or new gameplay mechanic is introduced.

## Two Kingdoms battlefield composition (September 2026)

### Lush island decorations — verified source mapping

The native `Terrain/Decorations/Rubber Duck/Rubber duck.png` is 96x32:
three horizontal 32x32 frames. Its Aseprite source confirms a 32x32 canvas,
three frames at 100 ms each. The previous automatic slicing incorrectly
split one ripple into a fourth sprite. Import the three complete fixed cells,
centered at 64 PPU, Point/uncompressed/Clamp/no mipmaps. Map them to the
ambient floating state only, looping at 10 FPS. Ducks are decorative water
animals, not food resources, units, blockers or vision sources.

All four `Rocks in the Water/Water Rocks_01..04.png` strips were inspected:
each is 1024x64 with sixteen 64x64 frames showing water ripples around a
stationary rock. Import fixed centered cells at 64 PPU and loop at 10 FPS.
`AmbientSpriteLoop` only changes the SpriteRenderer sprite; it never moves
objects or changes navigation, resources or combat.

All eight `Clouds/Clouds_01..08.png` are complete 576x256 illustrations,
not animation strips. Import each as one static Sprite, preserving detached
wisps and the painted shadow. The September 27 revision enlarges these same
whole sprites (1.1 scale, or 1.8 for small variants 03/08) and translates them
with a slow sinusoidal back-and-forth drift. This is procedural ambient motion,
not an invented sprite-sheet animation. Sixteen clouds stay inside map bounds;
two light clouds cross the ponds and others drift over coastal/interior groves.
Clouds have no collider, vision, navigation or gameplay authority.
The four ordinary Rock sprites are static, decorative boulders: larger ones
stay at the shoreline; smaller stones sit beside forest edges and away from
the main travel lane and Castle construction areas.

The expanded battlefield is 60x44 cells with two tiered Castle bases and a
flat central mine. Castle bases now have a 16x13 lower terrace at height 1 and
a nested 10x7 upper terrace at height 2, with two ascent routes per faction.
Each route uses two native ramp flights, one per height boundary. Each cliff uses the native
finished foot row (`r5c5/r5c6/r5c7`) once. The extra wall-extension row 4 is
omitted, eliminating the doubled visible face. Side ramps retain the verified
64x64 `r4c0/r5c0` (west) and `r4c3/r5c3` (east) pairs, matching Color3 and
their original same-column visual assembly. The verified runtime paths are
`Assets/Tiles/Terrain/Color3/Tilemap_color3_r4c0.asset`, `r5c0.asset`,
`r4c3.asset` and `r5c3.asset`; all reference centered 64x64 sprites from
`Assets/Terrain/Tileset/Tilemap_color3.png` at 64 PPU. The ramp halves remain
in one outside visual column, while the diagonally adjacent logical upper exit
must replace its left/right plateau edge with the matching center tile from
column 6. Interior exits use `r1c6`. South-edge exits use `r2c6`, and the cliff
cell immediately below uses `r5c6` instead of the closed `r5c5/r5c7` corner.
This is the verified Tiny Swords opening: the ramp plus an open surface
connection, with a second cliff connection piece only where a cliff is present.
Navigation is separate: each ramp uses explicit diagonally adjacent logical
endpoints so units follow the visible left/right slope while crossing each height
boundary. `DescendsRight` uses an upper-left to lower-right descent (and the exact
reverse ascent), while `DescendsLeft` mirrors that direction. Unit separation is
constrained to the authored axis for the short connector segment so it cannot
deflect this diagonal into a cliff.
Movement extends the two logical connector waypoints horizontally by 0.25 cell
inside their respective cells. The segment still crosses the cells' shared
corner, while its wider horizontal run keeps the unit ground pivot centered on
the painted slope in either direction. The two finished cliff rows are separated by a
walkable lower landing, never stacked into one wall. The mine remains flat.

Sheep retain the verified Idle/Grass mapping. There are 73: twelve above and
twelve below each Castle plateau, twelve neutral Sheep, and four at each base's
lower outer corners, plus two on Blue's upper court and three on its lower terrace.
Each base uses four existing Tree variants: two at the
lower terrace's northern rim (height 1) and two at the
southern corners of the lower terrace.
Blue additionally uses two native dark Tree1 pines on each tier, retaining the
existing Tree1/Stump1 mapping. Four edge clusters each contain three animated
Bushe3 grass tufts (the inspected dark spiky bush). Their ground roots use the
logical terrain elevation band and within-level Y order, with no colliders.
The upper court has one large Gold Stone 5 (catalog index 4) and one small
Gold Stone 2 (index 1), both inspected from their native PNGs. They retain their
existing matched highlights and resource capacities. Construction space and
both entrances remain clear. Dense woodland
uses the same four verified Tree/Stump pairs as real resource nodes. Only the
verified ambient Duck and water-rock animations described above are added;
no gameplay animation, recoloring or AI-created artwork is introduced.

Phase 6H.1 House-generated Sheep reuse this exact verified Idle/Grass/Move frame
mapping, 1.6 presentation scale and normal Sheep harvest presentation. They add
no new sprite, animation clip or animation-driven gameplay. House completion and
the 90-second replacement timer remain gameplay authority.

The supplied local Free Pack was visually inspected, including all terrain color
sheets, four trees, four bushes, four rocks, four water-rock sheets, eight clouds,
the Blue/Red Castle pair and Red Pawn Idle/Run. Existing gameplay animation
mappings below are retained. No sprite is AI-generated or recolored.

* `Tilemap_color1` supplies the main island, `color3` Castle plateaus and bright meadow
  patches, `color4` the flat mine clearing and
  approach paths, and `color5` cool coastal vegetation. All are 64x64 tiles.
* `Terrain/Decorations/Bushes/Bushe1.png`: Aseprite tag `Bush 1`, source frames
  0..7, exported sprites `Bushe1_0..7`, 100 ms each; LARGE scale 1.50.
* `Terrain/Decorations/Bushes/Bushe2.png`: Aseprite tag `Bush 2`, source frames
  8..15, exported sprites `Bushe2_0..7`, 100 ms each; SMALL scale 1.25.
* `Terrain/Decorations/Bushes/Bushe3.png`: Aseprite tag `Bush 3`, source frames
  16..23, exported sprites `Bushe3_0..7`, 100 ms each; LARGE scale 1.50.
* `Terrain/Decorations/Bushes/Bushe4.png`: Aseprite tag `Bush 4`, source frames
  24..31, exported sprites `Bushe4_0..7`, 100 ms each; SMALL scale 1.25.
  All four use Multiple sprites, 100 PPU, Point filtering, no mipmaps,
  uncompressed texture data and Clamp. The prior map setup selected only frame
  zero; Phase 6F.1 catalogs all eight verified frames and loops them at 10 FPS
  with deterministic phase offsets. Scaling affects a bottom-anchored visual
  child and never changes navigation occupancy.
* `Terrain/Decorations/Rocks/Rock1.png` through `Rock4.png`: static coastal
  boulders on nonwalkable water/shore fringe, away from travel corridors.
* `Terrain/Decorations/Rocks in the Water/Water Rocks_01.png` through `_04.png`:
  sixteen fixed 64x64 frames, looping ambient water ripples at 10 FPS; no collider.
* `Terrain/Decorations/Clouds/Clouds_01.png` through `Clouds_08.png`: full PNG single
  sprites at 64 PPU (automatic slicing had separated their tiny wisps), 16
  enlarged drifting clouds including two translucent pond clouds.
* All four verified Tree/paired Stump variants and all six Gold Stone variants
  are real resource nodes, using the existing capacities and animations.
* Blue/Red `Buildings/.../Castle.png` are the two gameplay headquarters. Other
  building sprites stay available through the existing construction UI; placing
  free starting production buildings would conflict with the starting rules.
* The Red Pawn animation set maps each Blue Pawn sprite to the same named
  imported sprite in the verified Red counterpart sheet. Idle has 8 frames,
  Run 6; existing work/carry/hammer frame ordering and timers are retained.
* `BattlefieldScenePreview` uses these same sprites to show the authored layout
  outside Play Mode. It is hidden before gameplay and stripped from builds.


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

One impact cycle per authoritative work event, followed by the verified Idle
Hammer recovery while construction remains active.

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
gameplay code. Repair uses a 0.8-second gameplay cycle with its successful
impact at 0.35 seconds; that semantic impact triggers the mild building pulse.

Phase 6H.5 keeps these exact Blue and Red frame mappings. The three-frame impact
plays once over 0.6 seconds, followed by 0.2 seconds of the mapped Idle Hammer
presentation. Hammer is never continuously looped during active work. The two
verified `PawnHammer` clips remain random variations; their playback is capped
at 0.7 seconds so a single Pawn's next 0.8-second work event cannot overlap the
previous tail. Animation frames and audio remain presentation-only and never
advance construction or apply repair.

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

The Blue and Red Monk folders contain `Idle.png` (1152x192, six frames),
`Run.png` (768x192, four), `Heal.png` (2112x192, eleven) and
`Heal_Effect.png` (2112x192, eleven). Phase 6C.2 verifies 192x192 full-cell
slicing and maps Heal to Monk action presentation; Heal_Effect is a target-side
one-shot only after HP is actually restored. PPU remains 100.

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
for the Guard button. The Phase UI/UX audit confirms these exact one-sprite
selection/production portrait mappings under
`Assets/UI Elements/UI Elements/Human Avatars/`:

| Unit | Texture | Imported sprite |
|---|---|---|
| Warrior | `Avatars_01.png` | `Avatars_01_0` |
| Lancer | `Avatars_02.png` | `Avatars_02_0` |
| Archer | `Avatars_03.png` | `Avatars_03_0` |
| Monk | `Avatars_04.png` | `Avatars_04_0` |
| Pawn | `Avatars_05.png` | `Avatars_05_0` |

The RTS HUD uses the nine verified slices in `WoodTable.png` for its persistent
resource panel. Their source geometry naturally assembles to 238x258 pixels,
but the compact HUD uses a 220x160 frame with proportionally reduced borders.
One stretched center surface sits behind the eight border pieces, so transparent
trim margins cannot expose the battlefield as grid seams. The same continuous
surface-plus-border construction uses `SpecialPaper.png` for selection and
command panels. No center or edge uses repeated tiling. `WoodTable_Slots.png`
supplies only the individually clickable command-button frame.
`Icon_01.png`, `Icon_02.png`, `Icon_03.png` and `Icon_04.png` represent
Population, Wood, Gold and Food. The command correction maps `Icon_05_0`
(`Assets/UI Elements/UI Elements/Icons/Icon_05.png`) to AttackMove and
`Icon_09_0` (`Assets/UI Elements/UI Elements/Icons/Icon_09.png`) to Eject All.
`Icon_09_0` remains the previously approved Rally world marker as well.
All listed UI textures are Sprite type, Point filtered, uncompressed, without
mipmaps, Clamp wrapped and imported at a Max Size that preserves the source.

Normal Blue/Red House variants, Barracks, Archery, Monastery, Tower and Castle
use a presentation-only 1.7 scale. Their imported center pivots and existing
sprite slicing remain unchanged. The visual child is shifted upward from the
sprite lower bound so its original ground contact stays fixed. Logical
`BuildingData` footprints remain House/Tower 2x2, Barracks/Archery/Monastery
3x3 and Castle 3x2. The Gold Mine is independently tuned at 2.60 and is
excluded from this common faction-building scale.

Pawn, Warrior, Archer, Lancer, Monk and any enabled Goblin unit continue using
their verified animation sheets, slicing, PPU and frame order. Runtime
`AnimatedVisual` children now use a common presentation scale of 1.6; no source
texture, sprite metadata, gameplay root or collider is rescaled. Per-frame
alignment offsets are multiplied by the same factor to retain the established
animation ground anchor.

Tree and Sheep visual children use the 1.6 presentation scale. Gold Stone 1-5
use 1.4, while Gold Stone 6 uses 1.55. Their verified sprites, animation frames,
PPU, root positions, colliders and logical occupancy remain unchanged. Building
assets are excluded and continue using their dedicated 1.7 profile or the Gold
Mine's dedicated 2.60 profile.

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
the default arrow. Runtime presentation enlarges all three full cursor textures
to 1.5 with nearest-neighbor sampling and scales their verified hotspots by the
same amount. Source PNGs and import metadata remain unchanged.

# 11. Phase 5A inspected economy visuals

These PNGs were inspected directly in the project. The Map test setup uses the
listed imported sprites; the Idle/Run and resource slicing listed here is unchanged.
Gameplay gathering uses `PawnGatherer` timers, independent of these visuals.

Phase 7A adds `Assets/UnitAnimations/RedPawn.asset` using the matching verified
Red Pawn sheets for Idle, Run, Axe, Pickaxe, Knife, carried Wood/Gold/Meat and
Hammer states. Slice names/order and 100 PPU imports are preserved. As with Blue,
the unrelated `Pawn_Run Hammer_5` fragment is excluded from the six-frame logical
Run Hammer sequence. Animation remains presentation-only; Red AI gathering uses
the same gameplay timers as Blue.

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
(`Gold_Resource_0`). Deposit feedback at the player Castle and completed Towers
uses the same verified icon mapping. Its presentation-only icon size is 0.60
world units and the `+amount` TextMesh uses font size 72. The deposit-only
layout keeps the icon and amount separated and vertically centered, appearing
1.9 world units above the Castle or Tower origin. Internal garrison
counts use font size 112; resource amounts, drop-off rules, capacity and garrison
capacity remain unchanged.

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
separate building-local anchors while a completed building is below 60 percent
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

## Phase 6G.2 formation command glyph inspection

The twelve imported Tiny Swords command icons at
`Assets/UI Elements/UI Elements/Icons/Icon_01.png` through `Icon_12.png` were
visually inspected before adding formation commands. Their verified subjects are
Wood, log, coin, meat, sword, shield, green directional arrow, red directional
arrow, crossed weapons, gear, alert and music. None confidently represents
Compact, Wedge, Loose or Line formation geometry.

Phase 6G.2 therefore uses small procedural UI dot layouts created by
`CommandPanelPresenter`; it does not map a misleading Tiny Swords icon and imports
no external art. The dots are UI presentation only and have no sprite, animation,
collider, gameplay or navigation authority.

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

## Phase 6C.2 verified Monk, Goblin, Gold Mine and TNT assets

| Use | Exact path | Source / verified frames |
| --- | --- | --- |
| Blue/Red Monk Idle | `Assets/Units/{Blue|Red} Units/Monk/Idle.png` | 1152x192, 6 frames of 192x192 |
| Blue/Red Monk Run | `Assets/Units/{Blue|Red} Units/Monk/Run.png` | 768x192, 4 frames of 192x192 |
| Blue/Red Monk Heal | `Assets/Units/{Blue|Red} Units/Monk/Heal.png` | 2112x192, 11 frames of 192x192 |
| Blue/Red Heal effect | `Assets/Units/{Blue|Red} Units/Monk/Heal_Effect.png` | 2112x192, 11 frames of 192x192 |
| Goblin Torch | `Assets/Units/Goblins Units/Goblins_Torch/Torch_Yellow.png` | 1344x960, 7x5 grid of 192x192. Exported PNG: Idle 0-6; Run 7-12; transparent padding 13; Attack Right 14-19; padding 20; Attack Down 21-26; padding 27; Attack Up 28-33; padding 34 |
| Goblin TNT | `Assets/Units/Goblins Units/Goblins_TNT/TNT_Red.png` | 1344x576, 7x3 grid of 192x192. Exported PNG: Idle 0-5; transparent padding 6; Run 7-12; padding 13; Throw 14-20. Dynamite is separate |
| Dynamite | `Assets/Units/Goblins Units/Goblins_TNT/Dynamite.png` | 384x64, 6 frames of 64x64 |
| TNT explosion | `Assets/Particle FX/Explosion_03.png` | 1728x192, 9 frames of 192x192, one-shot |
| Gold Mine inactive | `Assets/Terrain/Resources/Gold/Gold Mine/GoldMine_Inactive.png` | 192x128, single sprite |
| Gold Mine active | `Assets/Terrain/Resources/Gold/Gold Mine/GoldMine_Active.png` | 192x128, single sprite |
| Gold Mine wreck | `Assets/Terrain/Resources/Gold/Gold Mine/GoldMine_Destroyed.png` | 192x128, single sprite |
| Central plateau left stair, upper | `Assets/Terrain/Tileset/Tilemap_color2.png`, `Tilemap_color2_r4c0` | rect x=0, y=64, 64x64; PPU 64; centered pivot; descending-left upper cliff transition |
| Central plateau left stair, lower | `Assets/Terrain/Tileset/Tilemap_color2.png`, `Tilemap_color2_r5c0` | rect x=0, y=0, 64x64; PPU 64; centered pivot; descending-left lower slope/foot |
| Central plateau right stair, upper | `Assets/Terrain/Tileset/Tilemap_color2.png`, `Tilemap_color2_r4c3` | rect x=192, y=64, 64x64; PPU 64; centered pivot; descending-right upper cliff transition |
| Central plateau right stair, lower | `Assets/Terrain/Tileset/Tilemap_color2.png`, `Tilemap_color2_r5c3` | rect x=192, y=0, 64x64; PPU 64; centered pivot; descending-right lower slope/foot |

The official stair pieces are embedded in the 576x384 color terrain sheet even
though no standalone Stair, Steps or Ramp filename exists. They retain the
existing 64x64 slicing and sprite names, Point filtering, no mipmaps,
uncompressed import and Clamp wrap. The upper/lower pair replaces the relevant
cliff column instead of being placed over a solid wall. `Bridge_All.png` is a
separate bridge-art sheet kept locally for possible future use; it is ignored by
Git and has no active Gold Mine stair, scene, catalog or validator mapping.

All active Phase 6C.2 textures use Sprite type, Point filtering, no mipmaps,
uncompressed import, Clamp wrap and a Max Size at least as large as the source.
Unit, effect and Gold Mine textures in this section use PPU 100; the terrain
sheet and its stair slices retain terrain PPU 64. Torch has only Yellow and TNT
only Red in the inspected pack; these are neutral Goblin presentation colors,
not Blue/Red faction ownership. No compatible dedicated Goblin death animation
was found, so both use the verified generic Dead/Buried project VFX.

The source Aseprite timelines and the exported PNG cell indexes are different.
`Torch_Yellow.aseprite` has timeline tags Original 0-2, Idle 3-9, Run 10-15,
Attack Right 16-21, Attack Down 22-27 and Attack Up 28-33. The PNG omits
Original, starts each tag on a new seven-cell row and pads six-frame rows with a
transparent cell. `TNT_Red.aseprite` has Original 0, Idle 1-6, Run 7-12, Throw
13-19 and Dynamite 20-25. Its gameplay PNG omits Original and Dynamite, starts
each included tag on a new row and pads the two six-frame rows. Runtime mappings
therefore use the exported PNG indexes in the table, never raw timeline indexes.

Torch uses the verified Right, Down and Up attack rows. Left-facing attacks use
the existing horizontal flip; diagonal requests use the nearest verified
vertical cardinal row because the source has no separate diagonal tags. TNT has
one verified Throw row and uses horizontal flip only. Throw plays seven frames
at 10 FPS and releases its projectile at the start of zero-based frame 6
(0.60 seconds), when the held explosive disappears from the hand. The generic
Dead/Buried VFX remains responsible for death presentation.

## Phase 6H.3 Pawn Scout Fire Marker

`Assets/Particle FX/Fire.png` was inspected as a horizontal atlas. Its importer
contains fourteen sliced sprites: seven complete flame drawings plus seven small
detached particle slices. The Scout animation uses only the verified complete
flame drawings, ordered left to right by source rectangle:

| Playback index | Sprite name |
| --- | --- |
| 0 | `Fire_0` |
| 1 | `Fire_10` |
| 2 | `Fire_8` |
| 3 | `Fire_11` |
| 4 | `Fire_12` |
| 5 | `Fire_13` |
| 6 | `Fire_7` |

`Fire_0` is also the verified Scout command icon. Runtime plays the seven
frames at 10 FPS on a child renderer scaled to 0.70 and positioned above the
Pawn. The detached slices `Fire_1` through `Fire_6` and `Fire_9` are not used as
animation frames. The source PNG and `.meta` remain locally ignored by the
repository's existing Particle FX ignore rule; the tracked HUD catalog stores
their current GUID references.

## Phase 6H.4 Warrior Committed Attack Presentation

Phase 6H.4 continues to use the verified four-frame
`Assets/Units/{Blue|Red} Units/Warrior/Warrior_Attack1.png` mapping at 10 FPS.
No sprite sheet, slice or frame order changes. The 0.2-second hit point and
0.4-second committed duration are explicit gameplay data on
`WarriorCombatData`; animation frames remain presentation-only and cannot apply
damage. Holding `UnitState.Attacking` for that configured duration prevents the
existing Run transition from cutting off a legally started Warrior attack.

## Gameplay SFX Foundation / Initial Audio Integration

The following user-provided MP3 assets are referenced directly by
`Assets/Resources/Audio/GameplayAudioCatalog.asset`. Runtime code loads that
catalog once and never looks up an AudioClip by filename during an action.

| Audio event | Exact asset path | Authoritative gameplay trigger |
| --- | --- | --- |
| WarriorAttack | `Assets/Audio/SFX/Units/Warrior/sfx_sword_hit_01.mp3` through `_07.mp3` | Successful Warrior melee damage to a unit; one uniformly selected variation |
| LancerAttack | `Assets/Audio/SFX/Units/Lancer/sfx_lancer_attack_01.mp3` | Successful Lancer melee damage to a unit or building |
| ArcherAttack | `Assets/Audio/SFX/Units/Archer/sfx_arrow_sound.mp3` | Actual Archer projectile creation, including mounted Archer fire |
| MonkHeal | `Assets/Audio/SFX/Units/Monk/sfx_monk_heal_01.mp3` | A valid heal pulse that restores positive HP |
| PawnMining | `Assets/Audio/SFX/Units/Pawn/sfx_pawn_mining_01.mp3` | Successful Gold transfer during the existing gather impact |
| PawnChopping | `Assets/Audio/SFX/Units/Pawn/sfx_pawn_chopping_01.mp3` | Successful Wood transfer during the existing gather impact |
| SheepHurt | No active clip | Disabled following the explicit Sheep Hurt removal request |

The six active clips retain their source MP3 content. Import uses Decompress On Load,
preloaded audio data, background loading off and the existing source compression
quality. Source inspection found Archer, Lancer, Monk, Mining and Warrior encoded
as stereo; Chopping is mono. Stereo does not prevent distance attenuation,
but its positional image should be checked by listening in Play Mode.

The supplied Chopping file is approximately 54 seconds long and its first three
seconds contain no useful chopping impact. The catalog starts Chopping playback
at 3.0 seconds and applies a configurable 1.25-second presentation limit per
successful gather impact, so the useful sound plays without occupying a pooled
source for nearly a minute. Pawn Mining uses a substantially reduced provisional
volume of 0.20, while Pawn Chopping uses 0.65 so axe impacts remain clear. Sheep
harvesting no longer requests the Sheep Hurt cue and its former clip is absent.
`PawnFoodHarvest` is reserved in the catalog without a clip.

World SFX are camera-aware. Runtime rejects a cue whose source point is outside
the Main Camera viewport and stops a playing pooled source once camera movement
takes it off-screen. Sources still use the existing linear distance attenuation
inside the viewport and retain the established Fog visibility check.

## Phase 6E.2 verified building, Pawn, UI and match audio

The following mappings were verified from the actual MP3 filenames and their Unity
`.meta` GUIDs. No Gold Mine selection file exists in `Sound Effect`, so no substitute
mapping is invented.

| Audio event | Exact asset path | Approx. source duration / format | Runtime classification |
| --- | --- | --- | --- |
| CastleSelect | `Assets/Audio/SFX/Buildings/Sound Effect/castle_sound.mp3` | 0.862s, 44.1kHz stereo | 2D one-shot |
| HouseSelect | `Assets/Audio/SFX/Buildings/Sound Effect/house_sound.mp3` | 0.576s, 24kHz stereo | 2D one-shot |
| BarracksSelect | `Assets/Audio/SFX/Buildings/Sound Effect/barracks_sound.mp3` | 1.698s, 44.1kHz stereo | 2D one-shot |
| ArcherySelect | `Assets/Audio/SFX/Buildings/Sound Effect/archery_sound.mp3` | 0.836s, 11.025kHz mono | 2D one-shot |
| MonasterySelect | `Assets/Audio/SFX/Buildings/Sound Effect/monastery_sound.mp3` | 1.907s, 44.1kHz stereo | 2D one-shot |
| TowerSelect | `Assets/Audio/SFX/Buildings/Sound Effect/tower_sound.mp3` | 2.016s, 48kHz stereo | 2D one-shot |
| BuildingFire | `Assets/Audio/SFX/Buildings/Destruction/fire.mp3` | 36.519s, 44.1kHz stereo VBR | Positional owner loop |
| BuildingExplosion | `Assets/Audio/SFX/Buildings/Destruction/building_destroy.mp3` | 2.976s, 24kHz stereo | Positional one-shot |
| BuildingPlan | `Assets/Audio/SFX/Buildings/Construction/building_plan.mp3` | 0.984s, 48kHz stereo | 2D one-shot |
| PawnHammer primary | `Assets/Audio/SFX/Units/Pawn/sfx_pawn_building_01.mp3` | 1.032s, 24kHz stereo | Positional variation |
| PawnHammer variation | `Assets/Audio/SFX/Units/Pawn/sfx_pawn_building_02.mp3` | 1.200s, 24kHz stereo | Positional variation |
| CommandClick | `Assets/Audio/SFX/UI/Notification/click.mp3` | 0.078s, 44.1kHz stereo | 2D one-shot |
| CommandRejected | `Assets/Audio/SFX/UI/Notification/cant_click.mp3` | 0.456s, 48kHz stereo | 2D one-shot |
| UnitProductionFinished | `Assets/Audio/SFX/UI/Notification/finish_queue.mp3` | 2.116s, 44.1kHz stereo | 2D one-shot |
| ScoutPromotion | `Assets/Audio/SFX/UI/Notification/finish_queue.mp3` | 2.116s, 44.1kHz stereo | 2D promotion success one-shot |
| Victory | `Assets/Audio/SFX/Match/win.mp3` | 8.098s, 44.1kHz stereo | 2D one-shot |
| Defeat | `Assets/Audio/SFX/Match/defeat.mp3` | 11.760s, 24kHz stereo | 2D one-shot |

Short SFX use Decompress On Load with preloaded audio. Victory and Defeat use
Compressed In Memory with preload. The long Fire loop uses Streaming, background
loading and no preload. All source MP3 files remain unchanged. Fire uses
`AudioSource.loop`; it is not rescheduled as repeated one-shots.

`Assets/Audio/Music/attack_theme.mp3` was also found (about 10.971s, 44.1kHz
stereo), but Phase 6E.2 defines no attack-music state machine, so it remains
unmapped rather than being started from an invented gameplay rule.

## Phase 6E audio expansion mappings

| Audio event | Exact asset path | Authoritative gameplay trigger | Runtime classification |
| --- | --- | --- | --- |
| BuildingCompleted | `Assets/Audio/SFX/Buildings/Construction/building_completed.mp3` | First `UnderConstruction` to `Completed` transition | Fog-aware world one-shot |
| ArcherAttack | `Assets/Audio/SFX/Units/Archer/sfx_arrow_sound.mp3` | Actual projectile creation after ranged windup | Fog-aware world one-shot |
| ArcherArrowImpact | `Assets/Audio/SFX/Units/Archer/sfx_arrow_dmg.mp3` | Successful Archer projectile damage to a unit | Fog-aware world one-shot |
| WarriorAttack primary | `Assets/Audio/SFX/Units/Warrior/sfx_sword_hit_01.mp3` | Successful Warrior damage to a unit | Fog-aware world variation |
| WarriorAttack variations | `Assets/Audio/SFX/Units/Warrior/sfx_sword_hit_02.mp3` through `sfx_sword_hit_07.mp3` | Uniform random selection for the same successful hit event | Fog-aware world variation |
| GenericCommandVoice | `Assets/Audio/SFX/Commands/units_say_01.mp3` | Accepted Human command with at most three accepted Warrior/Lancer/Archer recipients; 30% roll and independent 30 game-second cooldown | 2D one-shot |
| ArmyCommandVoice | `Assets/Audio/SFX/Commands/army_say_01.mp3` | Accepted Human command with more than three accepted Warrior/Lancer/Archer recipients; 40% roll and independent 10 game-second cooldown | 2D one-shot |

Command categories are mutually exclusive. Pawn, promoted Pawn Scout and Monk are
eligible recipients for the generic response but never increase the army count.
Cooldown begins only when the centralized audio service accepts playback.
