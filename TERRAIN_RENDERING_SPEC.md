# Tiny Swords expanded battlefield

The map is 60x44 cells on a native 64x64 pixel tile grid. Each Castle has a
16x13 lower terrace and a 12x8 upper court. The central Gold Mine stays at height 0.
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
left pair r4c0/r5c0 or right pair r4c3/r5c3. Each visual endpoint is painted
at the corresponding logical stair cell. A route is on each flank, with a landing
between its flights. Closing either route must leave the other fully usable.
The central mine has no ramps.

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
