# Development Progress

## Current Phase

Phase 5C2 Tower/Castle Defense and Garrison passed final manual Play Mode validation.
Phase 5C2.1 presentation polish and targeted combat retaliation fixes passed final manual Play Mode validation.
Phase 5C1 passed. Phase 5A passed and Phase 5B1 remains stable.
The next gameplay phase has not begun.

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
- [x] User reports previous combat, selection, camera and clean Console manual Play Mode checks passed

## Phase 4B1 Unit Presentation and Warrior Guard

- [x] GAME_SPEC.md defines approved hold-to-Guard rules and damage formula; UI_SPEC.md defines presentation behavior
- [x] Verified Blue/Red animation sheets documented in ASSET_MANIFEST.md; fragmented sheets resliced into complete frames
- [x] Six data-driven animation sets and six test prefabs configured; Unity batch asset validation passed
- [x] Map scene serialized with animation components, Guard input, Canvas, EventSystem, and additional Blue Lancer / Red Archer test units
- [x] Runtime and Editor C# build: 0 errors, 0 warnings; Unity batch setup and asset validation passed
- [x] User reports previous unit animation, Guard and combat presentation manual Play Mode checks passed
- [x] User reports previous selection UI and floating damage manual Play Mode checks passed
- [x] User reports previous controls, navigation, terrain and clean Console manual Play Mode checks passed

## Movement Polish (before Phase 4B2)

- [x] Eight-direction A* respects Water, Cliff, permanent occupancy and stairless height transitions
- [x] Smooth world-space diagonal waypoints and distinct group destinations preserved
- [x] Configurable local separation for living units, without permanent grid occupancy
- [x] C# runtime and Editor build: 0 errors, 0 warnings
- [x] Unity batch validation: diagonal path/cost, blocked corners, height, group destinations and local separation passed before extending edge-case assertions
- [ ] Re-run `Tools > Tiny Kingdom > Validate Movement Polish` in Unity Editor for the added moving-group and Cliff/height separation assertions (second batch run stopped at Unity Licensing)
- [x] User-reported Movement Polish Play Mode validation passed except melee units competing for one target position
- [x] User reports melee engagement manual Play Mode validation passed

## Melee Engagement Slots (before Phase 4B2)

- [x] Eight angular reservations per target; Warrior/Lancer distance uses each unit's attack range
- [x] Slot candidates checked against walkability, permanent occupancy, height, A* reachability and final waypoint safety
- [x] Attack, Move, Guard, death and combat cancellation release reservations in code
- [x] Ranged Archer retains center-target chasing without melee reservations
- [x] Runtime and Editor C# build: 0 errors, 0 warnings
- [ ] Run `Tools > Tiny Kingdom > Validate Melee Engagement` in Unity Play Mode (batch Unity stopped at Licensing before validation)
- [x] User reports melee engagement behavior passed manual Play Mode validation

## Final Archer Release Timing Polish (before Phase 4B2)

- [x] Archer Shoot mapping checked: eight frames; attack playback adjusted to 6.667 FPS so the drawn-arrow frame is visible at the 0.7-second release
- [x] Adjusted gameplay release delay to 0.7 seconds per Play Mode feedback; attack cycle remains 1.2 seconds
- [x] Pending shot cancellation implemented for combat cancellation, Move, death and invalid target
- [x] Focused Play Mode validator added; runtime and Editor C# build: 0 errors, 0 warnings
- [ ] Run focused Archer release validation menu command in Unity Play Mode (not reported as run)
- [x] User reports Archer release timing manually verified in Play Mode
- [ ] Visually verify Arrow appears at the drawn bow during Shoot and Console remains clean

## Archer Projectile Arc Polish (before Phase 4B2)

- [x] Inspect Blue/Red Archer Shoot and Arrow assets; record the bow tip mapping in ASSET_MANIFEST.md
- [x] Launch Arrow from the mirrored bow offset and draw an arc that ends at the living target's current position
- [x] Extend the focused Archer validator to check launch point, arc, moving-target landing and impact damage
- [ ] Run `Tools > Tiny Kingdom > Validate Archer Release` in Unity Play Mode after the arc change
- [ ] Visually verify Blue/Red shots in both directions, the height of the arc, landing position and clean Console

## Phase 4B2 Combat Behavior

- [x] User reports prior movement, combat, animation, Guard, UI and melee engagement manual Play Mode validation passed; Archer release timing also manually verified
- [x] AttackMove gameplay API stores the destination, interrupts for valid nearby enemies and resumes after combat
- [x] Phase 6B.2.1 final binding: A then left-click valid ground issues AttackMove; Right Click cancels targeting and continues its normal contextual command
- [x] AttackMove requires a selected military unit; success or Escape ends targeting while an invalid destination keeps it armed
- [x] Initial Main Camera framing moved to include the Blue test units at Play start
- [x] Periodic deterministic auto acquisition for Idle and AttackMove using the active-unit registry and combat data radius
- [x] Auto targets require a reachable route or immediate valid attack range; unreachable, dead and invalid targets are released
- [x] Idle defensive anchor and AttackMove engagement leash limit automatic chasing; explicit Attack is exempt
- [x] Move, Attack, AttackMove and Guard cancel or replace lower-priority automatic combat; melee slots and Archer timing remain in use
- [x] Focused Phase 4B2 Play Mode validator added; runtime and Editor C# build: 0 errors, 0 warnings
- [ ] Run `Tools > Tiny Kingdom > Validate Phase 4B2 Combat Behavior` in Unity Play Mode
- [ ] Manually verify A then left-click, empty route, sequential enemies, normal Move near enemies, Guard, Archer, melee slots, leash, selection and clean Console
- [ ] Confirm Blue units can be selected immediately after entering Play Mode from the corrected initial camera view

## Do Not Start Yet

- Phase 6B fog-aware combat and later vision extensions
- Next gameplay phase after Phase 6A
- AI
- RL

## Phase 5A Economy Foundation

Status: **PASS** — user completed manual Unity Play Mode validation; Console had no errors.

- [x] Read GAME_SPEC.md economy values; Food 200, Wood 200, Gold 100; Pawn cost 50 Food; carry 10; Castle drop-off
- [x] Store the 1.5-second gather cycle and provisional Sheep/Tree/Gold capacities 60/100/100 in Phase5AEconomy ScriptableObject
- [x] Separate Blue and Red ResourceStockpile, explicit ResourceType, deterministic ResourceNode depletion
- [x] PawnGatherer capability uses existing UnitMovement and A*; cargo, return, deposit and command interruption implemented
- [x] Per-target reachable interaction reservations for resource nodes and Castle
- [x] Inspected Pawn Idle/Run and resource/Castle sprites mapped in ASSET_MANIFEST.md
- [x] Map Play Mode test setup includes three Blue Pawns, two of each resource, a Blue Castle, and a resource HUD
- [x] Mixed-selection Gather affects Pawns only; selection summary displays Pawn and cargo
- [x] Runtime and Editor C# build: 0 errors, 0 warnings; 18 scene sprite references and six resource positions checked statically
- [x] Verified Blue/Red Pawn Axe, Pickaxe, Knife and carry sheets; repaired only proven work-sheet slicing fragments
- [x] Added gameplay-side 0.45-second work impact in each 1.5-second cycle, verified resource icons, and matching carry visuals
- [x] Added three physical Castle front drop-off slots with 0.15-world-unit arrival tolerance and stockpile-triggered deposit feedback
- [x] Added weak explicit-command Pawn combat, damage flash, Tiny Swords health bars, and disabled combat damage-number subscribers in Map
- [x] Updated GAME_SPEC.md, ECONOMY_SPEC.md, UI_SPEC.md, and ASSET_MANIFEST.md for Phase 5A completion rules
- [x] Manual Play Mode: Pawn gathers Wood and physically reaches the Castle drop-off before depositing
- [ ] Run Tools > Tiny Kingdom > Validate Phase 5A Wood Loop in Play Mode; it checks Tree hover, generic Gather, cargo/depletion, and Castle stockpile deposit
- [x] Manual Play Mode: work, tool-idle and carry visuals, Castle arrival, resource popups, hit feedback and health bars work
- [x] Correct centered Tiny Swords sprite pivots for resource/Castle visuals; move gather endpoint near the reserved cell edge
- [x] Manual Play Mode: Pawn gathers Wood, Gold and Food only after physically reaching its reserved interaction position; no distant work animation
- [x] Manual Play Mode: the single-frame health bar and full-height fill work correctly on units
- [x] Manual Play Mode: gathering repeats every 1.5 seconds; work animations play naturally and recovery shows matching tool Idle
- [x] Manual Play Mode: carry visuals appear during transport, including return to Castle, and not during Gathering recovery
- [ ] Check three Pawns gathering the same Tree simultaneously in Play Mode
- [x] Manual Play Mode: Pawn selection ring placement is correct; existing military visuals remain correct
- [x] User completed remaining Phase 5A Play Mode validation: Pawn combat, targeting/death, combat/navigation/UI regressions and Console all passed
- [ ] Confirm provisional gathering rate and resource capacities after visual gameplay review

## Phase 5B1 Building Construction Foundation

Status: **STABLE per user report** — detailed Phase 5B1 checklist below remains unreported.

- [x] Verified House 100 Wood/+5 population, Barracks 150 Wood, Tower 100 Wood/50 Gold, and free MVP repair against GAME_SPEC.md
- [x] Recorded provisional HP, build seconds, footprints, and repair rate in separate BuildingData assets
- [x] Verified Blue/Red House, Barracks, Tower and Castle sprites and Blue Pawn Hammer sheets in ASSET_MANIFEST.md
- [x] Added placement UI, preview, grid footprint validation, resource spending, builder assignment, construction, building selection and repair commands
- [x] Added separate construction progress and damaged-building HP bars; full-HP completed bars are hidden
- [ ] Play Mode: valid House/Barracks/Tower placement, costs spent once, invalid terrain/occupied/outside-map rejection, cancel and insufficient funds
- [ ] Play Mode: Pawn reaches site, hammer cycle, pause/resume, multiple-Pawn deterministic assignment and A* rerouting
- [ ] Play Mode: House cap +5 once on completion, building selection/status, completed Tower without attacks, damaged-building repair
- [ ] Play Mode: Pawn gathering/combat, military commands, cursor, UI, camera and Console regression checks
- [ ] Review provisional HP, construction times, footprints and repair rate after visual test

## Phase 5C1 Unit Production, Building Roster and Resource Feedback

Status: **PASS** — automated checks and user-completed Unity Play Mode validation passed; Console had no errors.

- [x] Replace generic Barracks roster in GAME_SPEC.md with Castle Pawn, Barracks Warrior/Lancer, Archery Archer, Monastery future Monk
- [x] Set Pawn base damage to 2 without enabling Pawn auto acquisition
- [x] Map House1/House2/House3 as one House gameplay type with three visual choices
- [x] Add configurable Archery and Monastery construction data; mark their unspecified MVP values provisional
- [x] Add paid-at-enqueue FIFO production, population reservations and nearby grid-valid spawn search
- [x] Add Castle/Barracks/Archery production UI with cost, disabled state, queue and progress; Monk stays unavailable
- [x] Swap depleted Tree1 to verified Stump 1 and flash Sheep at successful Food impacts
- [x] Set Tower data to 300 HP, 75 Wood and unchanged 50 Gold
- [x] Runtime C# compile check: 0 errors, 0 warnings; asset reference checks completed
- [x] Play Mode: Castle trains Pawn, Barracks trains Warrior/Lancer and Archery trains Archer using existing gameplay systems and verified prefabs/data
- [x] Play Mode: costs are deducted once, population reservations/cap work, and multiple queued units train sequentially in FIFO order
- [x] Play Mode: blocked preferred spawn cells use valid fallback cells; units do not spawn inside footprints or invalid terrain
- [x] Play Mode: House A/B/C, Archery and Monastery construction passed; every House applies +5 population exactly once
- [x] Play Mode: Tree stump, Sheep harvest flash, depleted-resource behavior, Pawn damage 2 and Tower 300 HP / 75 Wood / 50 Gold passed
- [x] Play Mode: gathering, construction, repair, navigation, combat, selection, cursor, camera and UI regressions passed; Unity Console had no errors
- [x] Keep current production and construction times marked as provisional MVP values; Monk production remains disabled pending defined gameplay data

## Phase 5C2 Tower / Castle Defense and Garrison

Status: **PASS** — final manual Unity Play Mode validation completed; Console had no new errors or warnings.

- [x] Add data-driven Tower 2-internal/1-mounted and Castle 5-internal/3-mounted capacities
- [x] Add approach reservations, physical entry, worker cancellation with retained Pawn cargo, protected stationing and safe Eject All
- [x] Keep internal generic arrows separate from mounted Archer combat; 5 damage, no counters, projectile Guard mitigation
- [x] Preserve Archer Shoot windup/projectile/counters with mounted x1.20 final damage and provisional x1.25 range
- [x] Add deterministic nearest defensive targeting, 1.5-second provisional volleys and explicit projectile origins
- [x] Make completed Tower/Castle explicit combat targets with damage bars, hit feedback and destruction ejection
- [x] Add compact garrison count UI and contextual right-click priority; nearest units reserve slots deterministically
- [x] Add queryable unit/building vision-source registry without Fog-of-War rendering
- [x] Add Red Castle and completed Red Tower Map targets for manual building-combat validation
- [x] Runtime and Editor compile with all Phase 5C2 scripts: 0 errors, 0 warnings; Unity assembly reload succeeded
- [ ] Run `Tools > Tiny Kingdom > Validate Phase 5C2 Defense` in Unity Play Mode
- [x] Play Mode: Tower capacity/volleys/mounted Archer, protection, Eject All and destruction ejection
- [x] Play Mode: Castle full 5+3 occupancy, five-arrow volley, rooftop anchors and destruction ejection
- [x] Play Mode: Pawn cargo preservation, building attacks, production/economy/construction/repair/combat regressions and clean Console
- [ ] Visually review and tune provisional rooftop anchors, Castle HP, attack ranges, mounted range and volley interval

## Phase 5C2.1 Building / Resource / Combat Presentation Polish

Status: **PASS** - final manual Unity Play Mode validation completed; focused
presentation, economy, construction, retaliation, death VFX and regression checks passed.

- [x] Raise explicit Tower and Castle mounted-Archer anchors without changing unit pivots or combat values
- [x] Add internal-only world garrison count sourced directly from BuildingGarrison
- [x] Keep authoritative construction progress separate from HP and vertically separate world indicators
- [x] Add distinct perimeter reservations and additive BuildPower 1.0 per actively working Pawn
- [x] Add deterministic same-type resource chaining within configurable radius 6 and preserve partial cargo
- [x] Add explicit-Attack-only two-ring red world feedback that follows the target
- [x] Verify Sheep Idle/Grass/Move tags and add local FlatGround wandering that pauses for gathering
- [x] Map and animate Tree1-4 with matching Stump1-4 depletion visuals
- [x] Map Gold Stone1-6, exact 100/150/250/300/400/500 capacities and matching Highlight playback
- [x] Verify Dead/Buried source tags and add the faction-neutral Dead one-shot to actual unit HP death
- [x] Preserve garrison capacities/damage, mounted multipliers, production, combat, navigation and gather timing
- [x] Runtime and Editor compile checks: 0 errors, 0 warnings
- [ ] Run `Tools > Tiny Kingdom > Validate Phase 5C2.1 Presentation` in Play Mode
- [x] Visually validate Tower/Castle rooftop positions and internal world counts
- [x] Validate House/Barracks/Tower/Archery/Monastery progress bars and multi-Pawn build rates
- [x] Validate Wood/Gold/Food chaining, partial-cargo deposit and explicit-command interruption
- [x] Validate Sheep states, four Tree/Stump pairs, six Gold highlights/capacities and Dead VFX
- [x] Run Phase 5C2 defense, economy, production, construction, combat, input and clean-Console regression checks

### Phase 5C2.1 targeted fixes and combat retaliation

- [x] Add independent 10-second post-playback ambient Highlight timers to all six Gold variants
- [x] Lower only the Tower mounted Archer anchor from `(0, 0.93)` to `(0, 0.83)`; Castle anchors unchanged
- [x] Diagnose empty serialized Dead frame catalog and restore seven runtime Dead sprites
- [x] Preserve originating UnitController through melee and projectile damage
- [x] Add configurable Warrior/Archer/Lancer retaliation with 10-unit MVP leash; Pawn/Monk excluded
- [x] Preserve explicit Attack, Guard, Move, current-target and AttackMove priority rules
- [x] Runtime and Editor compile checks completed with 0 errors and 0 warnings
- [x] Play Mode: validate Gold ambient/mining/depletion timing and corrected Tower rooftop position
- [x] Play Mode: validate visible Dead VFX for scene and produced Pawn/Warrior/Archer/Lancer
- [x] Play Mode: validate retaliation, command priority, AttackMove resume, leash and no red command rings

## Phase 6A Team Vision and Fog of War Foundation

Status: **PASS** — implementation, compilation and user-completed Unity Play Mode
validation passed; Console had no new errors or warnings. Phase 6B has not started.

- [x] Add authoritative per-faction `Visible` / `Explored` / `Unexplored` state on the existing 40x28 logical grid
- [x] Reuse `IVisionSource`, `VisionSourceRegistry` and `BuildingVisionSource`; completed buildings register configurable vision
- [x] Balance unit radii to Pawn 3.0, Warrior 3.5, Archer 4.0 and Lancer 3.5; preserve House/Barracks/Archery/Monastery 5 and Tower 7; Phase 6B.1 adjusts Castle from 8 to 9
- [x] Use circular cell-center reveal, overlapping-source union, persistent exploration and a 0.10-second update interval
- [x] Add a point-filtered world-space fog texture with distinct Visible, Explored and Unexplored appearance
- [x] Hide unseen enemy units/buildings and their child world UI without changing their gameplay simulation
- [x] Block hidden enemy hover/Attack commands and suppress hidden enemy death presentation leaks
- [x] Hide Unexplored resources and Gold Highlight; keep discovered resources visible in Explored terrain for this MVP
- [x] Keep internal garrison occupants and mounted Archers from contributing independent vision
- [x] Add Blue/Red fog debug display, fog toggle and `Tools > Tiny Kingdom > Validate Phase 6A Vision`
- [x] Document Phase 6A rules and Phase 6B limitations in `VISION_FOG_SPEC.md`
- [x] Unity runtime and Editor script compilation: 0 errors; independent build check: 0 errors, 0 warnings
- [ ] Run `Tools > Tiny Kingdom > Validate Phase 6A Vision` in Unity Play Mode
- [x] Manual Play Mode: starting Castle reveal, moving-unit reveal and Visible-to-Explored transition
- [x] Manual Play Mode: Tower strategic reveal and Blue/Red visibility isolation
- [x] Manual Play Mode: enemy presentation hiding and UI/cursor/selection information-leak checks
- [x] Manual Play Mode: reduced unit radii and world-grid fog alignment
- [x] Manual Play Mode: pan/zoom and Phase 1–5 gameplay regression; Console had no new errors or warnings

## Phase 6B Fog-Aware Combat, Targeting and Combat Awareness

Status: **PASS** — Unity Play Mode validation completed before Phase 6C.

- [x] Add one authoritative `CombatVisibility` gameplay query backed by `TeamVisionSystem`
- [x] Require faction visibility for passive unit acquisition and AttackMove interruption
- [x] Drop normal unit and building targets when they leave faction vision
- [x] Resume saved AttackMove route after a fog-driven target loss
- [x] Cancel unreleased normal/mounted Archer and building-target windups on vision loss
- [x] Preserve already-released projectile resolution after vision loss
- [x] Require current faction visibility for Tower/Castle defensive volley targets
- [x] Require current faction visibility for mounted Archer acquisition and continuation
- [x] Preserve local direct-damage retaliation awareness without changing team vision or presentation
- [x] Preserve Pawn/Monk retaliation exclusions, RetaliationLeash 10 and command priority
- [x] Keep unit/building VisionRadius, acquisition radii, damage, timing and mounted/garrison values unchanged
- [x] Document Phase 6B combat-awareness rules in GAME_SPEC, VISION_FOG_SPEC and RL_SPEC
- [x] Add `Tools > Tiny Kingdom > Validate Phase 6B Combat Vision`
- [x] Run Phase 6B automated validator in Unity Play Mode
- [x] Manual Play Mode: hidden/visible passive acquisition and AttackMove acquisition/resume
- [x] Manual Play Mode: explicit Attack, melee cleanup and Archer pre/post-release vision loss
- [x] Manual Play Mode: Tower/Castle/mounted Archer targeting with hidden and allied-revealed enemies
- [x] Manual Play Mode: hidden-attacker retaliation, leash, no team reveal, Pawn, Guard and Move priority
- [x] Manual Play Mode: building target, Blue/Red symmetry, information leaks and full regression/Console

## Phase 6B.1 RTS Controls, Building UI, Castle QoL and Rally Points

Status: **PASS** — Unity Play Mode validation completed before Phase 6C.

- [x] Reserve A for Warrior/Archer/Lancer AttackMove and remove WASD camera movement
- [x] Add normalized Arrow-key and configurable 16-pixel eight-direction edge scrolling within existing bounds
- [x] Restore authoritative construction-progress presentation with an explicit lifecycle root separate from HP
- [x] Increase only Castle VisionRadius from 8 to 9; keep Tower 7 and unit radii unchanged
- [x] Increase garrison world count from font 32 / character size 0.035 to 48 / 0.05; Phase 6B.2 raises its bar clearance offset to +0.72 for the large Life Bar
- [x] Deposit Pawn cargo through CastleDropOff only after successful main-Castle internal entry
- [x] Add initial Rally Points for Barracks, Archery, Monastery and Tower; Phase 6C.1 later extends support to Castle/Home while House remains excluded
- [x] Reject invalid, blocked, unreachable and Unexplored Rally destinations without replacing the stored point
- [x] Add initial selection-only procedural Rally marker; Phase 6C.1 later replaces it with verified `Icon_07_0`
- [x] Make produced units and manual ejects use distributed normal Move destinations; destruction eject ignores Rally
- [x] Update GAME_SPEC, UI_SPEC, VISION_FOG_SPEC, ASSET_MANIFEST and Phase 6 validators
- [x] Run `Tools > Tiny Kingdom > Validate Phase 6B.1 RTS QoL` in Unity Play Mode
- [x] Manual Play Mode: Arrow keys, eight-direction edge scroll, A AttackMove and command cancellation
- [x] Manual Play Mode: all constructible progress bars and 1/2/3-Pawn accelerated progress
- [x] Manual Play Mode: Castle radius 9 and enlarged Tower/Castle internal counts
- [x] Manual Play Mode: Castle entry deposit, cancelled entry and Tower cargo preservation
- [x] Manual Play Mode: independent Rally flags, production movement, no-Rally behavior and multi-output distribution
- [x] Manual Play Mode: Tower manual eject follows Rally; destruction eject remains Idle
- [x] Manual Play Mode: Visible/Explored accepted, Unexplored/invalid Rally rejected, Castle excluded
- [x] Full production, construction, economy, garrison, combat, Fog, input and clean-Console regression

## Phase 6B.2 Movement, Facing, AttackMove Feedback and Building Damage Presentation

Status: **PASS** - Unity Play Mode validation completed before Phase 6C.

- [x] Change AttackMove confirmation to A + Left Click valid ground; invalid ground remains armed and Escape cancels
- [x] Add a fixed red two-ring AttackMove destination marker distinct from target-following explicit Attack feedback
- [x] Coalesce equivalent same-cell Move commands with a 0.2 world-unit epsilon
- [x] Remove the A* start-cell center from replacement paths so rapid commands do not return to an already-passed waypoint
- [x] Preserve UnitSeparation, logical A*, arrival tolerance and normal destination replacement
- [x] Face Pawn, Warrior, Archer and Lancer visual renderers from movement and target direction without flipping gameplay/UI roots
- [x] Face Pawn gather/build/repair and explicit knife work toward the interaction target
- [x] Preserve Lancer directional attack selection and horizontal flip for left-side equivalents
- [x] Map Fire_01/02/03 as 8/10/12-frame 64px, 10 FPS looping effects
- [x] Burn completed buildings only below 70 percent HP and stop Fire after repair to 70 percent or more
- [x] Map Explosion_01/02 as 8/10-frame 192px, 10 FPS one-shots and play both once on building death
- [x] Replace damaged-building bars with BigBar static frame plus one left-anchored continuous fill; keep unit SmallBar unchanged
- [x] Keep construction progress separate, raise garrison count clearance and suppress enemy damage VFX through Fog
- [x] Add `Tools > Tiny Kingdom > Validate Phase 6B.2 Presentation`
- [x] Run Phase 6B.2 automated validator in Unity Play Mode
- [x] Manual Play Mode: A + Left Click, invalid retry, Escape, red destination marker and Right Click cancellation followed by normal Move/Attack
- [x] Manual Play Mode: rapid nearby/same-direction path replacement without local backtracking or oscillation
- [x] Manual Play Mode: movement/attack facing for both factions and Pawn resource/build/repair facing
- [x] Manual Play Mode: all Lancer attack directions including flipped left-side equivalents
- [x] Manual Play Mode: building 80/69 percent HP, repair across 70 percent and no Fire during construction
- [x] Manual Play Mode: BigBar fill/visibility and Tower/Castle garrison-count stacking
- [x] Manual Play Mode: House/Barracks/Tower/Castle destruction, two one-shot Explosions and immediate gameplay cleanup/ejection
- [x] Manual Play Mode: hidden enemy Fire/Explosion, full Fog/gameplay regression and clean Console

## Phase 6B.2.1 Final Input, Building VFX and Building Health Bar Fixes

Status: **PASS** - Unity Play Mode validation completed before Phase 6C.

- [x] Confirm AttackMove with A + Left Click; keep invalid targeting armed and suppress selection for the targeting click
- [x] Make Right Click cancel armed AttackMove and continue the same normal contextual Right Click command
- [x] Centralize Small, Medium and Large building presentation profiles
- [x] Increase Fire and Explosion instance scales and spread their anchors by building size without changing imports or gameplay transforms
- [x] Expand the Big Life Bar runtime Fill from the 64 px source pattern to the measured 90 px inner container
- [x] Keep Fill left anchored with constant height; keep unit SmallBar and repair gameplay unchanged
- [x] Use compact Small/Medium building bars and the largest bar for Castle; retain garrison-count clearance
- [x] Add exact Git ignore rules for the unused Goblin building/unit, Gold Mine and Bridge assets without deleting local files
- [x] Extend the Phase 6B.2 validator with presentation-profile and exact Life Bar geometry checks
- [x] Run `Tools > Tiny Kingdom > Validate Phase 6B.2 Presentation` in Unity Play Mode
- [x] Manual Play Mode: validate A + Left Click, invalid Forbidden retry, Escape and Right Click contextual cancellation
- [x] Manual Play Mode: validate House/Tower, medium-building and Castle Fire/Explosion scale and anchors
- [x] Manual Play Mode: validate Big Life Bar at 100/90/50 percent, repair to full and Castle/other size hierarchy
- [x] Manual Play Mode: validate damaged Tower/Castle garrison stacking, Fog suppression, full gameplay regression and clean Console

## Phase 6C Match Flow, Victory / Defeat and Pixel-Art Result UI

Status: **NOT COMPLETE** - authoritative implementation and independent compile
checks are prepared; Unity Play Mode validation is still required. Rule-Based AI
and Reinforcement Learning have not started.

- [x] Add authoritative `Preparing -> Playing -> Ended` MatchManager state and `None / BlueVictory / RedVictory` result
- [x] Explicitly register the Blue and Red Main Castles by faction; ordinary buildings cannot end the match
- [x] Lock the first Main Castle death result and expose match-ended, winner and loser gameplay APIs
- [x] Block new player commands, production, gathering, construction, repair, garrison, Rally and new combat after match end
- [x] Preserve Castle ejection/destruction cleanup, already released projectiles, Fire/Explosion VFX and camera controls
- [x] Add a presentation-only 0.85-second result delay without changing `Time.timeScale`
- [x] Inspect Tiny Swords Ribbon/Banner/Button/Paper candidates and record exact mappings in `ASSET_MANIFEST.md`
- [x] Build centered Victory/Defeat UI from Blue/Red BigRibbons, SpecialPaper and matching Small Square button states
- [x] Add 0.55-alpha raycast-blocking overlay, selection clearing and gameplay-panel hiding
- [x] Reload the active scene for deterministic Restart reset
- [x] Add `Tools > Tiny Kingdom > Validate Phase 6C Match Flow`
- [x] Runtime and Editor independent C# compile checks: 0 errors, 0 warnings
- [ ] Run Phase 6C automated validator in Unity Play Mode
- [ ] Manual Play Mode: destroy Red Main Castle and verify one Blue Victory plus delayed VICTORY UI
- [ ] Manual Play Mode: destroy Blue Main Castle and verify one Red Victory plus delayed DEFEAT UI
- [ ] Manual Play Mode: destroy House/Barracks/Tower and verify the match continues
- [ ] Manual Play Mode: verify all post-match command, economy, production, construction, garrison and combat blocking
- [ ] Manual Play Mode: verify occupied-Castle ejection, Explosion visibility and no duplicate result callbacks
- [ ] Manual Play Mode: verify Ribbon/panel/button pixel clarity, overlay blocking, hover/pressed states and centered layout at 1280x720 and 2560x1440
- [ ] Manual Play Mode: Restart and verify clean resources, population, units, buildings, Fog memory, queues, garrison and result state
- [ ] Full economy, production, construction, repair, Fog, combat, AttackMove, garrison, Rally, Fire/Explosion, selection, camera and clean-Console regression

## Phase 6C.1 Fog Memory, RTS Information Rules and Presentation Polish

Status: **NOT COMPLETE** - implementation and independent Runtime/Editor compile
checks pass with 0 errors and 0 warnings; Unity Play Mode validation is still
required. Phase 7, Rule-Based AI and Reinforcement Learning have not started.

- [x] Play all fourteen verified Dead 0-6 then Buried 7-13 frames with exact 1.8-second source timing while gameplay death remains immediate
- [x] Route Warrior/Lancer unit and building melee attacks through the same target-agnostic `AttackPerformed` presentation event
- [x] Use the closest building combat collider point for facing while preserving Lancer directional attacks and horizontal flip
- [x] Extend Rally support to Castle/Home production and voluntary ejection; keep emergency destruction ejection independent
- [x] Replace the procedural Rally marker with verified neutral Tiny Swords `Icon_07_0`, one presentation-only marker per host
- [x] Separate player-facing destination knowledge from physical A* occupancy so hidden buildings do not leak through cursor/Move validation
- [x] Add separate Blue/Red last-seen building and resource snapshot dictionaries with no hidden live-reference presentation
- [x] Show static noninteractive ghosts only in Explored cells; remove/update them only after the location is Visible again
- [x] Keep enemy units ghost-free and require current visibility for live hover, selection and attack
- [x] Freeze Sheep/resource animation and suppress Gold Highlight in remembered presentation
- [x] Audit shadow architecture; preserve baked main sprites and darken only dedicated `Shadow.png`/`Shadow_L1`
- [x] Add `Tools > Tiny Kingdom > Validate Phase 6C.1 Fog Memory & Presentation`
- [x] Update GAME_SPEC, UI_SPEC, VISION_FOG_SPEC, ASSET_MANIFEST and RL_SPEC knowledge contract
- [x] Runtime and Editor independent C# compile checks: 0 errors, 0 warnings
- [ ] Run Phase 6C.1 automated validator in Unity Play Mode
- [ ] Play Mode: verify all fourteen death frames for scene and produced units, then VFX cleanup
- [ ] Play Mode: verify Warrior and multi-direction Lancer building attack facing/animation/damage timing
- [ ] Play Mode: verify Castle production Rally, voluntary/destruction ejection distinction, flag selection lifecycle and no duplicates
- [ ] Play Mode: verify unknown Castle hover/Move has no information leak, then normal interaction after discovery
- [ ] Play Mode: verify hidden building damage/repair/destruction stays frozen until re-scout
- [ ] Play Mode: verify Gold depletion, Tree-to-Stump and Sheep movement stay frozen in memory until re-scout
- [ ] Play Mode: verify enemy units disappear without ghosts and Blue/Red memory remains isolated
- [ ] Play Mode: visually verify dedicated terrain shadow darkness/alignment and confirm baked unit/building/resource sprites were not darkened
- [ ] Play Mode: run Fog, combat, Match Result, Restart and clean-Console regression tests
