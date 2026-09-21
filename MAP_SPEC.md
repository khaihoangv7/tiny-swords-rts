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

Different height levels require stairs.

Use A* pathfinding.

Unit navigation uses eight directions. Orthogonal steps cost 1.0 and diagonal
steps cost sqrt(2). A* uses Octile Distance as an admissible heuristic.

A diagonal step is valid only when both adjacent orthogonal side cells and both
possible two-step transitions through them are traversable. This prevents
corner cutting through Water, Cliff, occupied permanent blockers, or invalid
height changes. A direct diagonal height change also follows the normal Stairs
rule. Without confirmed Stairs, FlatGround cannot reach ElevatedGround.

The logical grid's Occupied flag represents permanent terrain/building
blockers. Moving or standing units do not set this flag. Living units use
small local world-space separation that stays on valid navigation terrain.

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
