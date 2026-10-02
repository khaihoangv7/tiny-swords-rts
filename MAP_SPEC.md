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
6. Elevated Ground L1 (Castle bases)
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
height = 1 or 2 on Castle terraces; the central lane/mine remains at height 0

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
nonwalkable and cannot authorize a transition. A Stair definition owns a
diagonally adjacent lower endpoint at height N and upper endpoint at height N+1, together with
its verified visual tile composition and orientation. Only that defined pair
may cross between the two height levels; there is no implicit height change.

Use A* pathfinding.

Unit navigation uses eight directions. Orthogonal steps cost 1.0 and diagonal
steps cost sqrt(2). A* uses Octile Distance as an admissible heuristic.

A normal diagonal step is valid only when both adjacent orthogonal side cells and both
possible two-step transitions through them are traversable. This prevents
corner cutting through Water, Cliff, occupied permanent blockers, or invalid
height changes. An authored side stair is one explicit diagonal height edge and
is validated by its reciprocal endpoint connection instead of the two same-height
side cells. FlatGround reaches ElevatedGround only through an explicitly configured
`Stairs`/height-connector cell.

Movement across a side stair follows that connector's diagonal axis in both
directions. `DescendsRight` moves from the upper-left endpoint to the lower-right
endpoint when descending and reverses the same line when ascending;
`DescendsLeft` is the horizontal mirror. Local unit separation must not bend
this short connector traversal into a vertical route or push the unit into the
adjacent cliff. Runtime movement extends both connector endpoints horizontally
inside their own cells. The resulting segment still passes through the exact
shared corner of the two legal cells, while its wider horizontal run keeps the
unit's ground pivot on the middle of the painted slope and produces the intended
on-screen diagonal in both ascent and descent.

The central objective is a flat clearing at height 0. The 2x2 mine starts
at (29,21), with open approaches from both factions and no mine-side ramps.
Blue's lower terrace occupies x=5..20, y=8..20 and Red's x=39..54, y=24..36
at height 1. The height-2 Castle courts are inset: Blue x=8..17, y=14..20;
Red x=42..51, y=30..36. Each 10 x 7 court is one cell narrower on both
sides than the previous court, begins two cells farther north, and extends to
the rear edge of its lower terrace. Single cliff rows sit immediately south of each tier;
the lower landing visibly separates them. `BattlefieldTerraces` defines all
four platforms and eight ramp flights: two independent ascent routes per base,
on opposite flanks, each connecting ground -> lower terrace -> upper court.
Each connector's lower and upper endpoints retain
an explicit partner: only that pair may cross the height boundary. Cliff faces
and diagonal shortcuts remain blocked.

The logical grid's Occupied flag represents permanent terrain/building
blockers. Moving or standing units do not set this flag. Living units use
small local world-space separation that stays on valid navigation terrain.

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

Building footprints may occupy FlatGround or ElevatedGround at any authored
height, including both Castle tiers. All footprint cells must share one height
and remain clear of resources, buildings and units. Water, Cliff, Stairs and
footprints spanning height boundaries are invalid. A selected Pawn must have
a route to an adjacent work cell at the building's height.

---

## First Map

Current expanded size:

60 x 44 cells (previously 40 x 28). `BattlefieldLayout.Width/Height` are the
shared authored dimensions. Terrain, navigation, camera bounds, zoom and fog
must cover the full map. Each Castle base has a 16 x 13 lower terrace and an
inset 10 x 7 upper court, with at least 171 unobstructed cells at Blue and 180 at Red across both tiers.

The current Two Kingdoms composition has an irregular, approximately mirrored
island shoreline. Its exact authored row insets are shared by
`BattlefieldLayout.IsLand` and baked separately into Tilemaps and navigation.
The central mine stands on flat ground; only the Castle bases are elevated.
Blue Castle is at (13, 19.5), Red Castle at (47, 35.5); each Castle is centered
left-to-right on its 10 x 7 upper court, with its 3 x 2 footprint occupying the
two northernmost rows.
Both start with three Pawns and open construction space. There are 24
Sheep per faction: twelve scattered on low meadows above its Castle plateau and
twelve below its cliff foot, plus twelve additional Sheep in neutral meadows.
Each Castle base also has two Trees along the lower terrace's northern rim and two at the
southern corners of its lower terrace. One large and one small Gold Rock sit at
the upper court's edges, with four Sheep near
the lower landing's corners. Blue additionally has two dark Tree1 pines on each
tier, two Sheep near the upper court's lower corners, and three Sheep on the
lower terrace (73 Sheep total). This keeps the Castle court and its starting
Pawn row clear. Four compact
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

---

## Second Map: Straight Lane

Map two is a simple linear battlefield on the shared 60 x 44 logical grid.
Blue occupies the left clearing and Red the right, at (6.5,23.5) and
(53.5,23.5). Each starts with one main Castle and three Pawns on height 2.
Following the user's revised reference, each base has a 15x16 height-1
rectangle (expanded from 13x12) and an outer-side 7x5 height-2 Castle court.
Blue: (3,15) and (3,21); Red: (42,15) and (50,21).
Each upper court is centered exactly on its Castle, and both bases mirror X.
First-tier construction space stays largely open. ALL Sheep occupy height 1,
hand-scattered into uneven groups north/south of the upper court rather than
straight rows. Small within-cell offsets retain authoritative occupancy cells.
Each side has 20 Sheep and six Gold Rocks; the flocks use different, irregular
positions rather than exact mirrored patterns. After the earlier relocation,
the latest four pink-marked Sheep are deleted (two per side), reducing 44 to 40.
The other Sheep keep their positions and identities.
Only Tree1 (dark pine) and Tree2 (bright pine) may be used on map two.
The October sketch removes the two outer H2 trees next to the Castles, adds
four trees along each northern tier rim, and six in the marked Blue flank/court
corners. Other tree positions and variants remain intact. Dense forest rows frame
the shoreline and both sides of the lane, separated by harvest aisles.
Additional pines line y=19/25 beside the road without occupying the road or mine clearing.
Additional H0 pines fill the shoreline and lane-edge gaps. The latest red-marked
northern bands add six pines per side; their existing H0 y=32 row moves to y=31,
with the end pine at local x=17 moved to x=16, leaving a continuous y=32 harvest
aisle reachable through local x=17. Other forest regions remain unchanged.
Blue has 93 trees, Red 87 (28/22 on the tiers); the slight asymmetry follows the user's marked
green regions. Food and Gold remain equal, with all resource work cells reachable.
The narrow H0 coastal aisles beside the enlarged platforms remain resource-free.
Each side's six Gold Rocks now form one compact H1 cluster with two large,
two medium, one small and one tiny native stone (variants 4/5/2/3/1/0), matching
the user's reference. Their existing per-variant capacities remain unchanged;
both factions have the same composition. The central Gold Mine is unchanged.
Four additional nonblocking bushes use Bushe1/2/3/4, one of each, scattered
across the bases and center. Six decorative Ducks float on water only; their
existing ambient animation has no collision, resource value or gameplay role.
Three yellow patches receive 24 additional varied static bushes. Two lower
land-edge patches receive six large/small decorative rocks; two marked water
patches receive four animated water rocks. Ten large/small whole-sprite clouds
drift slowly over woodland/coast, staying inside map bounds and keeping Castle,
Gold cluster, stair and central-mine views clear. All scenery stays nonblocking.

The broad base clearings occupy the left/right ends, connected ONLY by one
straight horizontal land corridor at y=17..27, widened to eleven cells.
The clear olive road is five cells wide at y=20..24; a round central island
of radius 8 contains a round mine clearing of radius 4.8. Both remain H0.
Water surrounds the two bases and strip; no separate flanking route exists.
The existing 2x2 Gold Mine stays at (29,21), with open passages on both sides.
Each of the four height gates (H0->H1 and H1->H2 for each faction) consists of
two adjacent identical native stair flights with the same orientation. The
user explicitly confirmed front/back copies touching, then requested moving
them one cell closer. Both copies use one X: Blue x=18 (H0/H1) and x=10 (H1/H2);
Red x=41/49. Complete pairs occupy y=20/21 and y=21/22, overlapping one cell.
Color3 ground behind their transparent pixels avoids a contrasting grass gap
at the joint. Each retains its complete native r4/r5 pair, without rotation,
rescaling or flipping within a gate. Gates stay on the inward side of each
expanded tier; no stairs are added to the southern cliff. Each gate has ONE
explicit bidirectional height connection at y=20/21. The rear visual copy's
upper cell y=22 is a same-height approach, not another height jump. Separate
visual layers preserve both full ramp copies without giving the shared cell
conflicting navigation heights. Closing the shared gate blocks passage;
neighboring cliffs and decorative ramps never permit shortcuts.
One finished cliff-foot row per tier leaves visible landings between levels.

`Assets/Scenes/LinearLane.unity`, `Assets/Data/LinearLaneLayout.asset` and
`Assets/Data/Navigation/LinearLaneNavigation.asset` are the map-two assets.
Their previous RiverMountains paths are migrated with GUIDs preserved. The
original map remains available separately. The map selection card is named
STRAIGHT LANE. The legacy editor builder can rebuild the scene through
`Tools/Tiny Kingdom/Build Map 2 - Straight Lane`.
