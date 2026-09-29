# Tiny Kingdom RL - Map Specification

## Tiny Swords

Asset:
Tiny Swords Free Pack by Pixel Frog.

Tile size:
64 x 64 pixels.

---

## Tilemap Layers

Render from bottom to top:

1. BG Color
2. Water Foam
3. Flat Ground
4. Meadows and Paths
5. Shadow L1
6. Elevated Ground L1 (Castle bases only)
7. Shadow and Elevated Ground L2 (upper Castle courts)
8. Decorations / Units / Buildings
9. Effects / drifting clouds

The current map has two Castle elevation levels and a flat central mine.
Each tier uses one row of native finished cliff-foot tiles, separated by a landing.
The old mine terraces and their offset ramps are removed.
See `TERRAIN_RENDERING_SPEC.md` for the exact rendering order.

---

## Logical Terrain

Each logical grid cell contains:

- x
- y
- terrain type
- walkable
- height level
- occupied

Terrain types:

WATER
FLAT_GROUND
CLIFF
ELEVATED_GROUND
STAIRS

Rules:

WATER:
not walkable

FLAT_GROUND:
walkable
height = 0

CLIFF:
not walkable

ELEVATED_GROUND:
walkable
height = 1 or 2 on the Castle terraces

STAIRS:
walkable
connects one explicit pair of endpoints at height N and N+1

---

## Navigation

Rendering must NOT determine pathfinding.

Use a separate logical navigation grid.

Units can move normally between cells at the same height.

Different height levels require an explicitly configured stair.

Normal movement connects cells only at the same height. A Cliff cell is
nonwalkable and cannot authorize a transition. A Stair definition owns both a
lower endpoint at height N and an upper endpoint at height N+1, together with
its verified visual tile composition and orientation. Only that defined pair
may cross between the two height levels; there is no implicit height change.

Use A* pathfinding.

Unit navigation uses eight directions. Orthogonal steps cost 1.0 and diagonal
steps cost sqrt(2). A* uses Octile Distance as an admissible heuristic.

A diagonal step is valid only when both adjacent orthogonal side cells and both
possible two-step transitions through them are traversable. This prevents
corner cutting through Water, Cliff, occupied permanent blockers, or invalid
height changes. A direct diagonal height change also follows the normal Stairs
rule. FlatGround reaches ElevatedGround only through an explicitly configured
`Stairs`/height-connector cell.

The central objective is a flat clearing at height 0. The 2x2 mine starts
at (29,21), with open approaches from both factions and no mine-side ramps.
Blue's lower terrace occupies x=5..20, y=8..20 and Red's x=39..54, y=24..36
at height 1. The height-2 Castle courts are inset: Blue x=7..18, y=12..19;
Red x=41..52, y=28..35. Single cliff rows sit immediately south of each tier;
the lower landing visibly separates them. `BattlefieldTerraces` defines all
four platforms and eight ramp flights: two independent ascent routes per base,
on opposite flanks, each connecting ground -> lower terrace -> upper court.
Each connector's lower and upper endpoints retain
an explicit partner: only that pair may cross the height boundary. Cliff faces
and diagonal shortcuts remain blocked.

The logical grid's Occupied flag represents permanent terrain/building
blockers. Moving or standing units do not set this flag. Living units use
small local world-space separation that stays on valid navigation terrain.

Building footprints may occupy FlatGround or ElevatedGround at any authored
height, including both Castle tiers. All footprint cells must share one height
and remain clear of resources, buildings and units. Water, Cliff, Stairs and
footprints spanning height boundaries are invalid. A selected Pawn must have
a route to an adjacent work cell at the building's height.

The Main Castle keeps its 3x2 gameplay footprint. Because its approved 1.7
presentation artwork extends beyond that footprint horizontally, its runtime
navigation occupancy adds one side-clearance cell on both the left and right.
The southern approach stays open for the existing drop-off and garrison slots.

Mobile neutral resources such as Sheep may change their occupied logical cell.
If this invalidates an active unit route, `UnitMovement` requests a replacement
A* route no more frequently than once per 0.35 seconds while blocked. Sheep do
not choose a wander destination currently containing a living unit. A unit
already caught inside a newly occupied start cell may leave that cell, but no
unit may enter another occupied cell.

---

## First Map

Current expanded size:

60 x 44 cells (previously 40 x 28). `BattlefieldLayout.Width/Height` are the
shared authored dimensions. Terrain, navigation, camera bounds, zoom and fog
must cover the full map. Each Castle base has a 16 x 13 lower terrace and an
inset 12 x 8 upper court, with at least 171 unobstructed cells at Blue and 180 at Red across both tiers.

The current Two Kingdoms composition has an irregular, approximately mirrored
island shoreline. Its exact authored row insets are shared by
`BattlefieldLayout.IsLand` and baked separately into Tilemaps and navigation.
The central mine stands on flat ground; only the Castle bases are elevated.
Blue Castle is at (12.5, 16.5), Red Castle at (47.5, 32.5); both start with three
Pawns and open construction space. There are 24
Sheep per faction: twelve scattered on low meadows above its Castle plateau and
twelve below its cliff foot, plus twelve additional Sheep in neutral meadows.
Each Castle base also has two Trees along the lower terrace's northern rim and two at the
southern corners of its lower terrace. One large and one small Gold Rock sit at
the upper court's edges, with four Sheep near
the lower landing's corners. Blue additionally has two dark Tree1 pines on each
tier and five Sheep near the upper court's edges (73 Sheep total). Four compact
clusters of three native Bushe3 tufts decorate Blue's terrace edges. These additions
leave the central building space and both stair routes clear, with at least
171 free cells at Blue and 180 at Red across the tiers. Two
small ponds break up the northern and southern woodland. Additional
large Gold Stones and the capturable Gold Mine make the central clearing the primary
economic objective. Static bushes are traversable ground cover; coastal rocks
remain outside movement corridors. Clouds are nonblocking overhead decoration.
Resource trees and gold are
real gatherable objects and occupy their logical cells. Dense paired woodlands
fill the surrounding island while leaving Castle skirts, pastures and the central
battle lane open; the whole resource layout must remain reachable. Woods now
use dense stands separated by harvest aisles. Sixteen decorative Ducks float
on the two ponds and coast; 48 animated water rocks, 16 enlarged drifting clouds and
additional forest/coastal stones enrich the island. Ambient objects have no
colliders, resource values, vision or gameplay behavior. Clouds move slowly back
and forth inside map bounds; one light cloud drifts over each pond, with others
above the interior groves and coastal land. The central mine remains uncovered.

`Assets/Data/TwoKingdomsLayout.asset` stores entity placement independently of
gameplay stats. `Tools/Tiny Kingdom/Build Two Kingdoms Battlefield` rebuilds the
authored scene; the earlier combat-test groups are retained but inactive.

The map should be approximately symmetrical.

Blue base:
bottom-left.

Red base:
top-right.

Each faction begins near:

- trees
- sheep
- gold

Map center contains:

- additional gold
- flat contested clearing with open approaches

Avoid unfair resource placement.
