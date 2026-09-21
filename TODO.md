# Development Progress

## Current Phase

Phase 4B1 presentation/combat and Movement Polish implemented; Unity Play Mode validation pending

## Done

- [x] Unity project created
- [x] Tiny Swords imported
- [x] Terrain sprites configured
- [x] Tile Palette created
- [x] Test map created
- [x] Camera controller
- [x] Logical 40x28 navigation grid
- [x] World/grid coordinate conversion
- [x] Eight-direction A* pathfinding with Octile heuristic, sqrt(2) diagonal cost and no corner cutting
- [x] Terrain height and Stairs transition rules
- [x] Future building occupancy support
- [x] Navigation Gizmo visualization
- [x] Pathfinding validation cases
- [x] Modular UnitController, UnitMovement and UnitSelection foundation
- [x] Blue/Red faction gameplay data
- [x] Idle and Moving gameplay states
- [x] Reusable Blue Warrior test prefab using a verified static sprite
- [x] Three Blue test units serialized on valid FlatGround
- [x] New Input System selection and move-command implementation compiles
- [x] A* move-command integration
- [x] Unique nearby destinations for group movement
- [x] Frame-independent waypoint movement validation
- [x] Water, Cliff and unreachable ElevatedGround command validation
- [x] Phase 3 Play Mode smoke test confirmed by user: selection, movement, camera and clean Console
- [x] Initial terrain visual layers loaded correctly in Unity Editor (116 shoreline foam cells and 56 offset shadow cells validated)
- [x] Cliff-foot contact shadow extension (8 additional cells) validated in Unity Editor
- [x] Terrain builder updated to preserve Phase 2/3 objects when rebuilding visual map
- [x] Terrain visual C# build: 0 errors, 0 warnings

## Terrain Visual Review

- [x] Unity Editor: `Tools > Tiny Kingdom > Validate Terrain Visuals` passed for 116 foam and 64 total shadow cells
- [x] Scene/Game View: foam and cliff-foot shadow review reported passed by the user
- [x] Play Mode: terrain visual polish validation reported passed by the user

## Phase 4A Combat Foundation

- [x] Warrior stats asset checked against GAME_SPEC.md (260 HP, 28 damage, range 1, interval 1.1 s, speed 3)
- [x] Blue and Red static Warrior sprites mapped from imported Tiny Swords idle sheets
- [x] Health, explicit Attack, chase, damage timer and death cleanup code compiles with 0 errors and 0 warnings
- [x] Map scene serialized with one idle Red Warrior on reachable FlatGround and the existing three Blue Warriors
- [x] Unity Play Mode verified by user: Blue selection, right-click Attack, navigation chase, stopping in range, repeated damage, Red death/disappearance, camera, movement, selection and clean Console

## Optional Additional Validation

- [ ] Run `Tools > Tiny Kingdom > Validate Phase 4A Combat` in Unity Play Mode (not reported as run)

## Phase 4B1 Unit Combat Types and Counter System

- [x] Warrior, Archer and Lancer gameplay data matches GAME_SPEC.md, including costs and movement speed
- [x] Blue and Red static Warrior, Archer and Lancer test prefabs use inspected Tiny Swords sprites
- [x] Archer projectile uses inspected Arrow sprite and authorized damage from CombatController
- [x] Counter multipliers and integer damage calculation validated deterministically
- [x] Deterministic matchups: one Warrior defeats one Lancer; two Lancers defeat one Warrior; Warrior defeats Archer at melee range
- [x] Map serialization preserves Phase 3 and Phase 4A test units, WaterFoam, Shadow_L1 and 40x28 navigation data
- [x] Runtime and Editor C# build: 0 errors, 0 warnings
- [ ] Run `Tools > Tiny Kingdom > Validate Phase 4B1 Combat` in Unity Play Mode
- [ ] Manually verify Warrior vs Warrior, Warrior vs Lancer, Archer projectile vs Lancer, selection, camera and clean Console in Play Mode

## Phase 4B1 Unit Presentation and Warrior Guard

- [x] GAME_SPEC.md defines approved hold-to-Guard rules and damage formula; UI_SPEC.md defines presentation behavior
- [x] Verified Blue/Red animation sheets documented in ASSET_MANIFEST.md; fragmented sheets resliced into complete frames
- [x] Six data-driven animation sets and six test prefabs configured; Unity batch asset validation passed
- [x] Map scene serialized with animation components, Guard input, Canvas, EventSystem, and additional Blue Lancer / Red Archer test units
- [x] Runtime and Editor C# build: 0 errors, 0 warnings; Unity batch setup and asset validation passed
- [ ] Unity Play Mode: verify Idle/Run/Attack/Guard and directional Lancer visuals, arrow visibility, sprite alignment
- [ ] Unity Play Mode: verify G/button hold and release, mixed selection, command cancellation, projectile timing and exact floating damage
- [ ] Unity Play Mode: verify selection summary/HP/death updates, existing controls, navigation, terrain visuals and clean Console

## Movement Polish (before Phase 4B2)

- [x] Eight-direction A* respects Water, Cliff, permanent occupancy and stairless height transitions
- [x] Smooth world-space diagonal waypoints and distinct group destinations preserved
- [x] Configurable local separation for living units, without permanent grid occupancy
- [x] C# runtime and Editor build: 0 errors, 0 warnings
- [x] Unity batch validation: diagonal path/cost, blocked corners, height, group destinations and local separation passed before extending edge-case assertions
- [ ] Re-run `Tools > Tiny Kingdom > Validate Movement Polish` in Unity Editor for the added moving-group and Cliff/height separation assertions (second batch run stopped at Unity Licensing)
- [ ] Unity Play Mode: verify diagonal route, repeated group movement and multiple melee attackers remain visually separated
- [ ] Unity Play Mode: verify Water/Cliff corners, Guard, attacks, Archer projectile, selection UI, floating damage, camera and clean Console

## Do Not Start Yet

- Phase 4B2 and later combat features
- Economy
- AI
- RL
