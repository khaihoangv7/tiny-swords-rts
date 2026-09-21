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

The central cliff face extends through y=12 and y=11. Its bottom row hides the footprint shadow. Add one contact shadow row at x=16..23, y=11, behind the cliff art. The verified Shadow sprite has visible pixels spanning roughly 80×80 px around the center of its 192×192 canvas, so about 8 px extend below the y=11 tile into the lower ground at y=10. This extra row is visual only; it does not alter the one-cell footprint offset or logical height.

## Cliff and stairs

Elevated top is walkable according to the separate navigation data. Cliff faces are not walkable. Cliff facing water may need different artwork from cliff facing lower land; exact variant mapping is not yet verified. The current central plateau faces lower land and keeps its existing Tiny Swords cliff pieces.

Stairs are supported by the logical navigation rules but are absent from the current map. Exact Tiny Swords sprite indices for the lower connection, elevated center connection, and cliff-adjacent connection remain **UNKNOWN**. Do not place stairs until those mappings have been inspected.

## Separation from navigation

`WaterFoam` and `Shadow_L1` are visual Tilemaps only. They do not change terrain type, walkability, height, occupancy, A*, or unit movement. Navigation remains in `Assets/Data/Navigation/MapNavigation.asset`; do not infer it from sprite pixels at runtime.
