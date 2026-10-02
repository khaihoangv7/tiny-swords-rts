# Tiny Swords expanded battlefield

The map is 60x44 cells on a native 64x64 pixel tile grid. Each Castle has a
16x13 lower terrace and a 12x8 upper court. The central Gold Mine stays at height 0.
Visual Tilemaps and baked navigation are separate.

## Layer order

BG_Color 0, WaterFoam 1, FlatGround 2, MeadowsAndPaths 3,
Shadow_L1 4, ElevatedGround_L1 5, Shadow_L2 6, ElevatedGround_L2 7,
ground decorations 6 (outside terraces), gameplay resources/buildings 10,
effects 18+, translucent clouds 19. Both maps use the two Castle elevations.
Map two retains its own independently authored platform and stair definitions.

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
ElevatedGround_L1/L2 and the front copy on StairFront_L1/L2 (orders 6/8).
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
