# Tiny Swords expanded battlefield

The map is 60x44 cells on a native 64x64 pixel tile grid. Each Castle has a
16x13 lower terrace and a 10x7 upper court. The central Gold Mine stays at height 0.
Visual Tilemaps and baked navigation are separate.

## Elevation-aware layer order (Phase 6F.1)

Gameplay world rendering uses one global projected ground depth. The authoritative
sort coordinate is `projectedY = groundAnchorY - logicalElevation * cellSize`.
It is quantized at 32 steps per navigation cell into the world range -2048..-506.
Elevation therefore changes the projected ground position instead of reserving
an absolute sorting band. A lower object in the screen foreground can correctly
render over a higher object behind it, while either object can pass behind the
other when their projected ground order reverses. Fixed category offsets 0..7
provide deterministic ties; mounted presentation uses offset 6.

Full terrain Tilemaps remain in Chunk mode and occupy background-only orders
-2080..-2049. Their top surfaces are never promoted above world entities. Only
the authored southern cliff-face cells and the sixteen verified stair halves
receive matching runtime `SpriteRenderer` foreground copies. These copies use
the same projected-depth formula: stairs use tie offset 0 and cliff faces use
offset 2. This keeps units above stair art at the same contact depth while still
allowing a front cliff face to occlude an entity behind it. Fog and established
presentation overlays remain at order 0 or above.

Dynamic units update from the grid cell beneath their movement root. Sheep update
only when their root position changes while wandering; idle dynamic entities skip
the depth calculation. Static Trees, Gold, buildings and decoration resolve once from
their gameplay root/ground anchor. Tall art is never sorted from its center or
top. Along a reciprocal height connector, presentation elevation interpolates
from the lower endpoint to the upper endpoint inside a 0.65-cell corridor. Since
the official stair endpoints change world Y and logical elevation by one cell,
both ends have the same projected depth and the transition is continuous in both
directions. Navigation data and movement waypoints remain unchanged.

## Platforms and ramps

`BattlefieldTerraces.Platforms` defines both tiers of the two Castle bases. Color3
supplies their surfaces, with proper edge/corner tiles from columns 5/6/7.
Use one row of finished cliff feet (`r5c5/r5c6/r5c7`) immediately below each
surface. Do not stack row 4 and row 5; that doubles the visible cliff height.

The two ascent routes per Castle each contain two flights using matching Color3 native
left pair r4c0/r5c0 or right pair r4c3/r5c3. Each flight is painted as one
native same-column pair immediately outside the matching plateau edge. Its
logical lower endpoint is in that outside column and its logical upper endpoint
is the diagonally adjacent walkable surface cell. The edge tile at that upper
exit must be replaced by the Color3 center connection tile; leaving `c5/c7`
there recreates the invalid red-X seam from the official guide. Interior exits
use `r1c6`. A south-edge exit beside a cliff uses `r2c6` at the walkable top and
`r5c6` directly below, replacing the closed `r2c5/r2c7` plus `r5c5/r5c7`
corner. The full cliff/side border must never continue through a stair opening.
Rendering never infers the elevation edge. A route is on each flank, with a landing between its flights.
Closing either route must leave the other fully usable.
The central mine has no ramps.

`LinearLane.unity` follows the user's revised reference: two 15x16 lower bases
with outer 7x5 upper Castle courts centered on their Castles, connected by one eleven-cell horizontal
strip and a round central mine island. Color1 renders ground/shore, Color4
the five-cell road and round mine clearing, Color3 both Castle tiers.
Each gate is two adjacent identical native ramp pairs of the same orientation,
not opposite slopes meeting at a point. The latest approved front/back copies
share one X per gate, with endpoint rows 20/21 and 21/22 overlapping one cell
to bring them closer. Blue gate X is 18/10, Red 41/49. Color3 r1c6 backs
transparent ramp corners on MeadowsAndPaths, eliminating contrasting grass
at the seam. Both full native r4/r5 pairs remain intact: the rear copy is on
ElevatedGround_L1/L2 and the front copy on StairFront_L1/L2. The front
Tilemaps render above the other terrain layers but below depth-sorted units,
so a unit crossing the ramp stays visible.
The inward-side entries follow the expanded rectangular tiers, with no
extra side landings or stairs along the southern cliff.
Blue retains the native descending-right pair; Red the descending-left pair.
Navigation is baked separately with four explicit endpoint pairs at y=20/21,
one per shared gate. The rear copy's upper cell is a same-height approach.
Tree1/Tree2 woodlands border the land and lane. Following the October sketch,
two outer H2 pines are removed, four northern-rim pines added per side and six
added in Blue's marked flank/court corners. The latest northern red bands add
six H0 pines per side and bring the existing y=32 row inward to y=31, leaving
y=32/local x=17 open for harvesting: 93/87 overall, 28/22 on the tiers.
Other regions keep their pine placement/variants. The four latest pink-marked
Sheep are deleted, leaving 20 per faction (40 total); remaining H1 flocks keep
their irregular positions.
Six differently sized native Gold Stones form one close H1 cluster per side.
Four new static Bushe1/2/3/4 decorations render at order 8. Six water-only Ducks
reuse the existing three-frame ambient loop, order 3 and scale 1.6. No resource
covers a gate/road; decorations have no colliders or navigation occupancy.
The three yellow patches add 24 mixed Bushe1/2/3/4 sprites at order 8. Six
varied static Rock1/2/3/4 sprites form the two marked land-edge patches; four
Water Rocks_02/04 ambient loops form the water patches at order 3. Ten whole
native large/small cloud illustrations drift at order 19 with alpha 0.62-0.72,
52-79 second cycles and native 64 PPU, without covering gameplay landmarks.
Phase 6H.2 stabilization preserves these verified ramp and connection tiles and projected sorting.
Only the two reciprocal logical endpoints are reserved as the connector corridor;
debug endpoint/corridor Gizmos are optional and disabled by default.

Shadow.png stays at native 192x192, 64 PPU, beneath the plateau. Shadows are
offset one cell south with contact shadow at the single cliff foot. The lower
ramp cell omits shadow that could obscure its entry. Water Foam retains its
16 frames of 192x192 at 10 FPS, around the island and ponds. These layers have
no collision. Imports remain Point, uncompressed, Clamp and without mipmaps.

Most Sheep and gatherable forests occupy low ground. Each Castle also has two
Trees along the lower terrace's northern rim and two at its southern corners.
The upper court has one large and one small native Gold Rock;
four Sheep graze at the lower landing's corners. Castle
construction space and main roads remain clear. Blue has two additional dark
Tree1 pines per tier and five Sheep on the upper court. Four compact clusters
of three animated Bushe3 tufts sit near Blue's terrace edges. Bush roots use
their logical elevation and Y order; they have no collision or navigation data.
Camera bounds, maximum zoom and overview export use
`BattlefieldLayout.Width/Height`; fog matches the baked navigation grid.

Ambient water decorations use fixed-cell native strips: Ducks have three
32x32 frames, water rocks sixteen 64x64 frames, both at 10 FPS and 64 PPU.
They remain anchored on water with no gameplay components. Whole cloud
illustrations translate back and forth with `AmbientCloudDrift`, using 48-78
second cycles and a small vertical drift. Full sprite bounds stay one cell
inside the map. Scale is 1.1 (1.8 for smaller variants 03/08), compared with
the previous 0.6/1.1. Two pond clouds use alpha 0.62; interior clouds 0.72;
coastal clouds 0.9. No sprite-sheet animation is invented. Forest stones remain
small nonblocking scenery.

## Animated bushes and harvest feedback (Phase 6F.1)

Bushe1 and Bushe3 are large at visual scale 1.50. Bushe2 and Bushe4 are small
at visual scale 1.25. Each verified eight-frame Aseprite tag loops at its source
100 ms interval. Position and variant produce a deterministic 0..0.799 second
phase offset. A shared lightweight scheduler advances the sprite loops without
a per-frame scene scan or LINQ allocation. The root stays fixed while a
bottom-anchored visual child scales and animates.

A successful positive Wood `ResourceNode.Take` emits a presentation event.
`TreePresentation` refreshes one 3.5 degree, 0.22 second damped wobble on the
visual child. It never queues impacts, moves the gameplay root, rotates the
selection hit area, or changes gathering/audio. Completion, disable and
depletion restore the exact cached local rotation.
