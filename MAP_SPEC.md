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
4. Shadow L1
5. Elevated Ground L1
6. Shadow L2
7. Elevated Ground L2
8. Decorations
9. Units / Buildings
10. Effects

MVP requires only one elevated level.

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
height = 1

STAIRS:
walkable
connects height 0 and 1

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

The central objective plateau has two fair south-face connectors at x=17 and
x=22. For each connector, y=11 is `Stairs` at height 0 and y=12 is `Stairs` at
height 1. The shared connector definition also names the exact upper/lower
terrain sprites, so a legal navigation transition cannot exist without its
visible stair configuration. All remaining cliff-face cells stay blocked. Blue
and Red therefore have separate shortest approaches to the plateau while the
adjacent cliff sides remain non-traversable and the existing no-corner-cutting
rule still applies.

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

---

## First Map

Initial size:

40 x 28 cells.

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
- elevated terrain
- strategic choke point

Avoid unfair resource placement.
