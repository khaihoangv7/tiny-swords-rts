# Tiny Swords Terrain Rendering

## Grid and layer order

The logical terrain grid uses 64×64 pixel tiles. Render the current map from back to front:

1. `BG_Color`
2. `WaterFoam`
3. `FlatGround`
4. `Shadow_L1`
5. `ElevatedGround_L1`

Higher elevations, if added later, repeat Shadow then Elevated Ground. The MVP uses only level 1.

## Verified source assets

| Visual | Source | Import structure | Use |
| --- | --- | --- | --- |
| Water background | `Assets/Terrain/Tileset/Water Background color.png` | 64×64, one sprite, PPU 64 | Lowest layer, including nonwalkable water |
| Water foam | `Assets/Terrain/Tileset/Water Foam.png` | 3072×192, 16 frames of 192×192, PPU 64 | Animated shoreline effect below ground |
| Shadow | `Assets/Terrain/Tileset/Shadow.png` | 192×192 composite, one imported sprite, PPU 64 | Offset visual depth below elevated ground |
| Ground colors | `Assets/Terrain/Tileset/Tilemap_color1.png` through `Tilemap_color5.png` | 576×384 sheets, imported as selected 64×64 terrain sprites | Ground and cliff artwork |

Do not reslice Water Foam or Shadow into 64×64 tiles. Their visible pixels occupy only part of the 192×192 canvas and align around the center of a logical cell.

## Shoreline foam

Place Water Foam on cells whose terrain art touches water. The current island occupies visual cells x=2..37, y=2..25, so its 116 perimeter cells carry foam. The layer sits below Flat Ground; the terrain covers the foam center while its rim extends into water. Foam is not placed in open water or inland. The 16 verified frames play at 10 FPS. Synchronized frame starts are accepted for this small technical map.

## Elevated shadow

The current walkable elevated footprint is x=16..23, y=13..19. Place the corresponding 8×7 shadow footprint at x=16..23, y=12..18: exactly one logical cell below it. The shadow is beneath Elevated Ground and has no collider.

The central cliff face extends through y=12 and y=11. Its bottom row hides the footprint shadow. Add contact shadow tiles at y=11 behind intact cliff columns only. The stair openings at x=17 and x=22 omit this contact shadow so a dark square does not visually close the slope. The verified Shadow sprite has visible pixels spanning roughly 80×80 px around the center of its 192×192 canvas, so about 8 px extend below the y=11 tile into the lower ground at y=10. This extra row is visual only; it does not alter the one-cell footprint offset or logical height.

## TINY SWORDS TERRAIN COMPOSITION

Reference: [Pixel Frog Tiny Swords Tilemap Guide](https://pixelfrog-assets.itch.io/tiny-swords/devlog/1138989/tilemap-guide).

The project follows the official Pixel Frog tilemap guide with a 64x64 logical placement grid. Terrain sheets contain separate center, edge and corner pieces; a single center grass tile must not be stretched or reused for every border. `PaintPlatform` currently chooses top-left/top/top-right, left/center/right and bottom-left/bottom/bottom-right pieces for both Flat Ground and the elevated upper surface.

Elevated terrain is composed from a walkable grass surface plus nonwalkable cliff art beneath exposed edges. The current central plateau faces lower land and uses the lower-land cliff group in `Tilemap_color2.png`. The visible cliff cells are not treated as walkable upper terrain.

The official stair art is embedded in `Assets/Terrain/Tileset/Tilemap_color2.png`; it is not a standalone `Stair.png`. Each route uses two 64x64 sprites that replace the upper and lower cliff pieces, so no solid cliff remains behind the slope:

- left route x=17: upper `Tilemap_color2_r4c0`, lower `Tilemap_color2_r5c0`;
- right route x=22: upper `Tilemap_color2_r4c3`, lower `Tilemap_color2_r5c3`.

Both routes use y=11 at height 0 and y=12 at height 1. Their orientation is explicit in `StrategicObjectiveMapLayout`; the same definition supplies sprite names and navigation cells. The adjacent x=16,18,21,23 cliff columns keep their normal cliff pieces and remain blocked. `Bridge_All_0` is not a stair and is not used by the Gold Mine plateau.

Shadow remains one layer below elevated terrain, with overlapping composite art preserved at its native 192x192 import. Water Foam remains its own animated layer below Flat Ground and keeps its 192x192 frame slicing. A future height level repeats the `Shadow_Ln` then `ElevatedGround_Ln` pattern. Stairs are currently the only explicit walkable connector between adjacent height levels.

## Separation from navigation

`WaterFoam` and `Shadow_L1` are visual Tilemaps only. They do not change terrain type, walkability, height, occupancy, A*, or unit movement. Navigation remains in `Assets/Data/Navigation/MapNavigation.asset`; do not infer it from sprite pixels at runtime.
