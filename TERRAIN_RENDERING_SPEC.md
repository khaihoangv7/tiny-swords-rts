# Tiny Swords expanded battlefield

The map is 60x44 cells on a native 64x64 pixel tile grid. Each Castle has a
16x13 lower terrace and a 12x8 upper court. The central Gold Mine stays at height 0.
Visual Tilemaps and baked navigation are separate.

## Layer order

BG_Color 0, WaterFoam 1, FlatGround 2, MeadowsAndPaths 3,
Shadow_L1 4, ElevatedGround_L1 5, Shadow_L2 6, ElevatedGround_L2 7,
ground decorations 6 (outside terraces), gameplay resources/buildings 10,
effects 18+, translucent clouds 19. The unused L3 objects are removed.

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
of three Bushe3_0 tufts sit near Blue's terrace edges at sorting order 8;
these static decorations have no collision or navigation data.
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
