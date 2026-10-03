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

- [x] Recorded the original Phase 5A baseline: Food 200, Wood 200, Gold 100; Pawn cost 50 Food; carry 10; Castle drop-off (carry/drop-off superseded by Phase 6H.1)
- [x] Store the 1.5-second gather cycle and Sheep/Tree/base Gold capacities 200/100/100 in Phase5AEconomy ScriptableObject (Tree/base Gold remain provisional)
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

- [x] Verified the original Phase 5B1 House 100 Wood/+5 population baseline (House cost superseded by Phase 6H.1), Barracks/Tower costs and free MVP repair
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
- [x] Add initial selection-only procedural Rally marker; Phase 6C.1 later replaces it with `Icon_09_0` after targeted visual review
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
- [x] Burn completed buildings only below 60 percent HP and stop Fire after repair to 60 percent or more
- [x] Map Explosion_01/02 as 8/10-frame 192px, 10 FPS one-shots and play both once on building death
- [x] Replace damaged-building bars with BigBar static frame plus one left-anchored continuous fill; keep unit SmallBar unchanged
- [x] Keep construction progress separate, raise garrison count clearance and suppress enemy damage VFX through Fog
- [x] Add `Tools > Tiny Kingdom > Validate Phase 6B.2 Presentation`
- [x] Run Phase 6B.2 automated validator in Unity Play Mode
- [x] Manual Play Mode: A + Left Click, invalid retry, Escape, red destination marker and Right Click cancellation followed by normal Move/Attack
- [x] Manual Play Mode: rapid nearby/same-direction path replacement without local backtracking or oscillation
- [x] Manual Play Mode: movement/attack facing for both factions and Pawn resource/build/repair facing
- [x] Manual Play Mode: all Lancer attack directions including flipped left-side equivalents
- [x] Manual Play Mode: building 61/59 percent HP, repair across 60 percent and no Fire during construction
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
- [x] Replace the procedural Rally marker with inspected Tiny Swords `Icon_09_0`, one presentation-only marker per host
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

### Phase 6C.1 targeted presentation corrections

- [x] Prevent repeated building-range `movement.Stop()` calls from resetting Warrior/Lancer attack presentation every frame
- [x] Change Rally marker from the incorrectly identified `Icon_07_0` to requested crossed-weapons `Icon_09_0`
- [x] Increase dedicated terrain Shadow alpha from 128/255 to 153/255 while preserving baked gameplay sprites
- [ ] Play Mode: verify Warrior and Lancer visibly complete attack clips against buildings
- [ ] Play Mode: verify `Icon_09_0` Rally marker lifecycle and final Shadow_L1 readability

### Phase 6C.1 circular Fog hotfix

- [x] Confirm authoritative TeamVisionGrid already uses cell-center Euclidean radius checks with fractional radii
- [x] Center building vision on the complete logical footprint
- [x] Replace the 40x28 presentation texture with a reusable 160x112 circular visual mask
- [x] Preserve separate Blue/Red visual exploration history and authoritative Fog memory rules
- [x] Add 0.25-cell presentation feather without expanding authoritative gameplay vision
- [x] Rebuild the mask only for dirty/quantized source changes and reuse all runtime buffers
- [x] Extend Phase 6A and Phase 6C.1 validators for circular geometry, mask resolution and building centers
- [ ] Run both Fog validators in Unity Play Mode
- [ ] Play Mode: verify Pawn, Archer, Tower and Castle circles plus multi-source union and movement trails
- [ ] Profile moving groups for Fog CPU, texture upload cost, GC allocation and FPS regression

### Phase 6C.1 health relation and impact presentation additions

- [x] Resolve SmallBar and Big/Large Life Bar fill color from owner faction relative to the shared `PlayerViewFaction`
- [x] Use green `Color32(74, 201, 86, 255)` for friendly fills and red `Color32(255, 62, 62, 255)` for enemy fills without changing frame or fill geometry
- [x] Keep live enemy bars Fog-gated and keep all health/status UI out of last-seen building ghosts
- [x] Add one reusable ground-anchored squash/stretch controller for Pawn, Warrior, Archer, Lancer and Sheep visuals
- [x] Trigger unit deformation only from positive actual HP loss and Sheep deformation only from successful Food transfer
- [x] Preserve gameplay roots, colliders, shadows, selection rings, health bars, facing and active animation states
- [x] Runtime and Editor compile: 0 errors, 0 warnings after the health/reaction additions
- [ ] Run the extended Phase 6C.1 automated validator in Unity Play Mode
- [ ] Play Mode: verify friendly unit/building bars are green and visible enemy unit/building bars are red, including both Castles
- [ ] Play Mode: verify no health/status bar remains on an Explored enemy building ghost
- [ ] Play Mode: verify unit hit flash plus squash/stretch/recovery while Idle, moving, attacking, Guarding and gathering
- [ ] Play Mode: verify Lancer directions/facing, rapid-hit restart, feet anchoring and no transform drift
- [ ] Play Mode: verify one Sheep flash and squash/stretch per successful Food gather impact with no position drift
- [ ] Play Mode: verify lethal damage remains immediate and Dead/Buried VFX retains priority
- [ ] Full Fog, combat, gathering, construction, production, garrison, Rally, Match Result and clean-Console regression

## Phase 6C.2 Monk, Gold Mine and Neutral Goblin Objective

Status: **NOT COMPLETE** - implementation and independent Runtime/Editor compile
checks pass with 0 errors and 0 warnings; Unity asset setup, automated validator
and manual Play Mode validation remain required. Phase 6D, Phase 7 and RL have
not started.

- [x] Add Monk data and Monastery FIFO production: 80 HP, 1 population, speed 3, 60 Food + 60 Gold, 7 seconds
- [x] Add deterministic same-faction auto/manual healing: +5 HP, 4-second interval, range 2, auto detection 4
- [x] Play verified Monk Heal and Heal_Effect only on an actual successful heal
- [x] Add explicit Goblin faction relationships without Castle, economy, population or match-result participation
- [x] Add one Torch and one TNT guard near the central objective with deterministic guard targeting/leash
- [x] Add Torch Warrior-baseline combat and delayed fractional 1-percent regeneration
- [x] Add deterministic Dynamite projectile, one-shot Explosion_03, unit-only 18-damage AoE and safe 0.60 knockback
- [x] Add neutral/owned/depleted/destroyed Gold Mine state authority with 1500 HP, 2000 Gold and 2x2 occupancy
- [x] Add 3-Pawn internal capacity, 15 Gold per Pawn per 5 seconds, partial final payout and Eject All
- [x] Preserve internal Pawn population; depletion ejects safely and combat destruction kills internal Pawns only
- [x] Reuse building combat, health bar, 0.60 profile Fire, Explosion_01/02 and persistent wreck presentation
- [x] Extend Fog visibility and last-seen memory to Gold Mine without Goblin unit ghosts or hidden-state leaks
- [x] Increase both Main Castles from 1000 to 2000 HP while preserving the shared Fire threshold
- [x] Remove obsolete broad Goblin/Gold Mine ignore rules and explicitly unignore active runtime textures
- [x] Verify and document exact asset paths, dimensions, frames, tags, variants and import requirements
- [x] Runtime and Editor independent C# compile checks: 0 errors, 0 warnings
- [ ] Run `Tools > Tiny Kingdom > Prepare Phase 6C.2 Strategic Objective` in Unity after import/recompile
- [ ] Run `Tools > Tiny Kingdom > Validate Phase 6C.2 Strategic Objective`
- [ ] Manual Play Mode: Monk production, auto priority, manual heal, effect timing, cancellation, faction/Fog rules
- [ ] Manual Play Mode: mine capture, 1/2/3-Pawn payout, partial depletion, safe ejection and persistent ownership
- [ ] Manual Play Mode: enemy-owned mine attack, 60-percent Fire, internal-only Pawn death, building Explosion and wreck
- [ ] Manual Play Mode: Torch attack/regen and TNT throw/projectile/AoE/no-friendly-fire/safe knockback/leash
- [ ] Manual Play Mode: Gold Mine current/last-seen Fog behavior and Goblin current-visibility-only behavior
- [ ] Manual Play Mode: Castle 2000 HP/result behavior and full combat/economy/production/navigation/UI regression
- [ ] Confirm Unity Console has no new errors or warnings

## Phase 6C.2.1 Goblin Fog, Gold Mine Access and Objective Presentation

Status: **NOT COMPLETE** - focused implementation and static validation are
prepared. Unity Editor validator and the requested manual Play Mode checks remain.
Phase 6D, Phase 7, AI and RL have not started.

- [x] Make Goblin Torch/TNT start hidden until current player vision is evaluated
- [x] Gate the full Goblin renderer hierarchy without disabling gameplay
- [x] Prevent creation-frame leaks from Goblin Heal, Dynamite and TNT explosion presentation
- [x] Keep moving Goblins out of Fog last-seen memory
- [x] Add two symmetric explicit height connectors at x=17 and x=22, y=11..12
- [x] Preserve blocked cliff sides, eight-direction A* and no-corner-cutting rules
- [x] Replace the incorrect `Bridge_All_0` overlays with verified Tiny Swords terrain stair pairs from `Tilemap_color2.png`
- [x] Recompose both south cliff openings from upper/lower 64x64 stair tiles without stretching or a solid wall behind them
- [x] Share stair cells, orientation and visual sprite names through one explicit connector definition
- [x] Remove Phase 6C.2 catalog/runtime/validator dependencies on `Bridge_All_0` and restore its intentional Git ignore rules
- [x] Update terrain shadow repair/validation to omit cliff-foot shadow at the two stair openings
- [x] Increase all Gold Mine state visuals uniformly from 1.00 to 1.30 with ground correction
- [x] Keep Gold Mine logical footprint 2x2 and collider 1.75x1.25 unchanged
- [x] Update Gold Mine Fire scale/anchors and Explosion scale for the larger visual
- [x] Move the two Goblin guards outside a 2.0 world-unit visual clearance
- [x] Extend Phase 6C.2 validator for Fog gates, connectors, Blue/Red paths, cliff blocking and scale
- [x] Audit Torch/TNT PNG dimensions and Aseprite tags; document the exported row layout separately from raw timeline indexes
- [x] Import the complete Torch 35-cell and TNT 21-cell 192x192 grids while preserving Point, no mipmaps, uncompressed, Clamp and 100 PPU
- [x] Remove transparent padding cells from Goblin Idle, Run, Attack and Throw animation mappings
- [x] Map Torch Right/Down/Up attacks from verified source rows and leave TNT without invented directional rows
- [x] Align TNT projectile release to Throw frame 6 at 0.60 seconds without changing combat balance
- [x] Extend validation for exact slice rectangles, pivots, animation indexes, prefab animation references and runtime AnimatedVisual state
- [ ] Run `Tools > Tiny Kingdom > Prepare Phase 6C.2 Strategic Objective` after Unity recompiles
- [ ] Run `Tools > Tiny Kingdom > Validate Phase 6C.2 Strategic Objective` in Edit Mode and Play Mode
- [ ] Manual Play Mode: Goblin unseen/reveal/hide/re-reveal and hidden regeneration VFX
- [ ] Manual Play Mode: Blue/Red access, group stair traversal, cliff blocking and Goblin return path
- [ ] Manual Play Mode: Gold Mine inactive/active/destroyed scale, Fire, Explosion and Fog ghost alignment
- [ ] Manual Play Mode: Pawn entry, guard clearance, performance markers and clean Console

## Phase UI/UX RTS HUD

Status: **NOT COMPLETE** - runtime and Editor compile checks pass with 0 errors
and 0 warnings. Unity asset preparation, automated validation and manual Play
Mode inspection remain required. No later gameplay, AI or RL phase has started.

- [x] Inspect and document WoodTable, SpecialPaper, WoodTable_Slots, resource icons and exact Human Avatar mappings
- [x] Add one persistent lower-left WoodTable panel for Population Used/Cap, Wood, Gold and Food from authoritative data
- [x] Add a contextual SpecialPaper selection panel for units, multi-unit summaries, buildings and resources
- [x] Add a contextual SpecialPaper command grid for AttackMove, hold-to-Guard, Pawn builds, production and Eject All
- [x] Reuse current command, cost, population, placement, queue and garrison gameplay APIs
- [x] Add resource and neutral Gold Mine selection without bypassing Fog visibility rules
- [x] Remove Goblin Torch/TNT from current runtime spawn and active validation requirements while retaining their assets/code
- [x] Add `Tools > Tiny Kingdom > Prepare RTS HUD`
- [x] Add `Tools > Tiny Kingdom > Validate RTS HUD`
- [x] Runtime and Editor independent C# compile checks: 0 errors, 0 warnings
- [x] Fix legacy SelectionSummary event cleanup to prevent MissingReferenceException after selecting a unit
- [x] Diagnose the WoodTable 238x258 source geometry and remove the undersized nine-cell assembly
- [x] Add a 1920x1080 responsive full-width 1896x188 safe HUD region with a shared baseline and 12-unit margins
- [x] Replace tiled WoodTable/SpecialPaper centers with one continuous surface plus simple border pieces
- [x] Size Resources to 220x188, keep Selection at 380x188 and adapt Commands from 240x188 through a 1288x188 safe maximum
- [x] Use one row of shared command buttons adapting from 104x112 down to 76x82
- [x] Extend the validator for anchors, viewport bounds, panel/button sizes, continuous surfaces and raycast blocking
- [ ] Run `Tools > Tiny Kingdom > Prepare RTS HUD` after Unity recompiles
- [ ] Run `Tools > Tiny Kingdom > Validate RTS HUD` in Edit Mode and Play Mode
- [ ] Manual Play Mode at 1280x720, Scale 100%: verify pixel clarity, anchors, no stretching or panel overlap
- [ ] Manual Play Mode at 2560x1440, Scale 100%: verify pixel clarity, anchors, no stretching or panel overlap
- [ ] Verify population/resources update live from authoritative gameplay values
- [ ] Verify no-selection, single-unit, multi-unit, building and resource selection panel states
- [ ] Verify unit/build/production/Eject All command availability, disabled states, hotkeys and costs
- [ ] Verify hold-to-Guard remains active for the complete mouse hold and AttackMove retains existing behavior
- [ ] Verify building placement, production, economy, combat, Fog, cursor, camera, Match Result and clean Console regressions

## Phase 6D.3 - Command Tooltips and Cursor Interaction Polish

Status: **NOT COMPLETE** - implementation and independent Runtime/Editor
compile checks pass with 0 errors and 0 warnings. Edit Mode/Play Mode validation
and manual visual tests remain required before PASS.

- [x] Root-cause selected-building Forbidden cursor as implicit rally-point validation taking priority over selectable hover
- [x] Restore explicit target mode > selectable Hand > normal terrain Default cursor priority
- [x] Add one persistent SpecialPaper command tooltip above the command panel
- [x] Add pointer triggers to all reusable command slots, including disabled buttons
- [x] Read Train costs/stats/times from UnitCombatData and Build values from BuildingData
- [x] Derive Warrior/Archer/Lancer matchup labels from CombatDamage.Multiplier
- [x] Describe Pawn worker and Monk healer roles from current gameplay behavior
- [x] Reuse RtsHudAssetCatalog Food/Wood/Gold/Population icons with affordability coloring
- [x] Clear tooltip on pointer exit, selection change, command reuse/hide, match end and HUD teardown
- [x] Replace the fixed 460x250 tooltip with compact 240/286/320-width content sizing and auto height
- [x] Remove inactive Cost, Stats and Details sections from vertical space calculation
- [x] Follow the Input System pointer with a 30-unit offset while visible only
- [x] Flip left/below at screen edges and clamp inside an 8-unit safe margin
- [x] Keep SpecialPaper frame fitted to the computed root and all tooltip raycasts disabled
- [x] Add `Tools > Tiny Kingdom > Validate Phase 6D.3 UI`
- [x] Run runtime and Editor C# compile checks with 0 errors and 0 warnings
- [ ] Run Phase 6D.3 validator in Edit Mode and Play Mode
- [ ] Manual Play Mode: verify Train Pawn/Warrior/Archer/Lancer/Monk tooltip content
- [ ] Manual Play Mode: verify House A/B/C, Barracks, Archery, Monastery and Tower tooltip content
- [ ] Manual Play Mode: verify disabled/unaffordable/full-population command hover and red cost styling
- [ ] Manual Play Mode: verify rapid hover/selection, match end/restart and no tooltip flicker or stale content
- [ ] Manual Play Mode: verify Barracks/House shrink, Warrior/Tower growth and no clipped wrapped text
- [ ] Manual Play Mode: verify cursor following plus left/below flipping and clamping at screen edges/corners
- [ ] Manual Play Mode: verify tall-to-short and short-to-tall transitions do not retain stale root size
- [ ] Manual Play Mode: verify Castle-selected same/other-building Hand cursor regression
- [ ] Manual Play Mode: verify placement/AttackMove override, Fog anti-leak and UI-over-world cursor behavior
- [ ] Manual Play Mode: verify Blue controls, Red AI, buildings and Console remain regression-free

## Phase 6D.4 - Interaction Cursor and Multi-Builder Animation Polish

Status: **NOT COMPLETE** - implementation, documentation and independent Runtime/
Editor compile checks pass with 0 errors and 0 warnings. Unity validator and manual
Play Mode tests remain required before PASS.

- [x] Diagnose Castle/Gold false Forbidden states as sprite coverage falling outside root colliders
- [x] Diagnose Sheep remainder as animated frame/pivot changes leaving its initial hit area stale
- [x] Diagnose Tree remainder as Wood being omitted from authoritative ResourceNode hit-area setup
- [x] Resolve normal observable entity hover before raw ground movement validity
- [x] Preserve UI > Placement/AttackMove > normal selectable hover > terrain priority
- [x] Add sprite-bound trigger-only `SelectionHitArea` without changing gameplay colliders
- [x] Add selection coverage to Sheep, every Tree variant, Gold resources, Gold Mine, Castle and constructed buildings
- [x] Convert current world sprite bounds into SelectionHitArea local collider space
- [x] Resynchronize animated Sheep/Tree hit areas after presentation frame changes
- [x] Use a reusable non-alloc cursor overlap buffer with trigger and all-layer coverage
- [x] Verify cursor hotspots remain 22/17 Default, 24/18 Hand and 32/31 Forbidden before 1.5x scaling
- [x] Add a small symmetric 0.08-world-unit Sheep-only selection padding for visible edge pixels
- [x] Keep padding out of gameplay collider, gather range, slots, navigation and resource rules
- [x] Resolve selection and contextual commands through the hit-area target root
- [x] Preserve Fog/resource observability and friendly-building faction checks
- [x] Remove the incomplete diagnosis that only the 0.5-second/0.8-second visual cadence caused the multi-builder bug
- [x] Diagnose secondary IdleHammer as terminal UnitSeparation displacement exceeding PawnBuilder's former 0.1 arrival check
- [x] Align builder arrival with a small 0.2 terminal tolerance while still requiring movement completion and a reserved slot
- [x] Loop verified Hammer frames while each Pawn's authoritative `IsWorking` state is true
- [x] Preserve RunHammer during approach and IdleHammer before active participation
- [x] Preserve construction progress, duration, interaction slots and work-cycle SFX
- [x] Stop Hammer presentation through existing leave/death/reassignment/completion lifecycle
- [x] Preserve repair Hammer animation and authoritative repair impact
- [x] Add `Tools > Tiny Kingdom > Validate Phase 6D.4 Interaction Polish`
- [x] Run independent Runtime and Editor C# compile checks with 0 errors and 0 warnings
- [ ] Run Phase 6D.4 validator in Edit Mode and Play Mode
- [ ] Manual: Pawn selected, Sheep lower/center/upper/left/right visible artwork shows Hand
- [ ] Manual: Pawn selected, Tree trunk/center/top/left/right canopy shows Hand for every variant
- [ ] Manual: right-click through Sheep/Tree expanded hit areas resolves normal Gather command
- [ ] Manual: Pawn selected, every Gold variant center/upper artwork shows Hand
- [ ] Manual: Castle center/left/right visible artwork shows Hand; just outside returns terrain cursor
- [ ] Manual: UI blocking, Placement, AttackMove and Fog-hidden object cursor priority
- [ ] Manual: one, two and three-plus builders all report AtSlot/IsWorking/contributor/Hammer after reaching distinct slots
- [ ] Manual: staggered arrival, Move-away, death and late join update animation independently
- [ ] Manual: all builders stop on completion and repair animation still works
- [ ] Compare construction duration before/after with equal builder count
- [ ] Verify gathering, Castle drop-off, building placement, combat, Red AI and audio regressions
- [ ] Confirm Unity Console has no new errors or warnings

## Phase 6D.5 - HUD Scale and Adaptive Command Panel Polish

Status: **NOT COMPLETE** - implementation, documentation and independent Runtime/
Editor compile checks pass with 0 errors and 0 warnings. Unity validation and
manual Play Mode visual tests remain required before PASS.

- [x] Preserve fourteen reusable command actions and four formation actions
- [x] Increase every shared command button from the initial 60x58 through 76x72 and 88x82 to 104x112
- [x] Increase aspect-preserving command icons from the initial 30x30 through 44x44 and 52x52 to 68x68
- [x] Keep every supported visible-command count from 1 through 14 on exactly one row
- [x] Grow the Command frame horizontally before shrinking all active cards uniformly
- [x] Keep Command height fixed at 188 for every active-command count
- [x] Lock Resources at x=0/220x188 and Selection at x=224/380x188 independently of Command count
- [x] Restore Command to fixed HUD x=608 with lower-left anchor/pivot and rightward-only growth
- [x] Calculate Command maximum width from the fixed left edge to the safe right boundary
- [x] Avoid hierarchy rebuilds and skip layout work when count/width are unchanged
- [x] Keep Selection at 380x188 while increasing single portrait to 96x96 and title/details to 24/16
- [x] Adapt multi-selection rows inside the fixed panel: 52x52/21 for two types, 40x40/19 for four and 34x34/18 for five
- [x] Derive Command width only from active actions: minimum 240, two commands 240, six 678, seven 788, reference cap 1288
- [x] Adapt icons 68→48, labels 15→12, secondary costs 13→10 and hotkeys 13→10 with card size
- [x] Preserve readable 13/14-action cards at approximately 91.69x98.81 and 84.71x91.34
- [x] Add a padded CommandContent rect so every active card remains inside the frame
- [x] Keep the Command left edge stable and resize the actual surface/raycast blocker with the frame
- [x] Increase Resources panel to 220x188, rows to 36, icons to 34x34 and values to bold 22
- [x] Preserve WoodTable/SpecialPaper continuous surfaces, frame pieces and UI raycast blocking
- [x] Preserve the 1920x1080 CanvasScaler, tooltips, UI audio and command authority
- [x] Add `Tools > Tiny Kingdom > Validate Phase 6D.5 HUD Scale`
- [x] Document Phase 6D.5 layout rules in `UI_SPEC.md`
- [x] Run independent Runtime and Editor C# compile checks with 0 errors and 0 warnings
- [ ] Run the Phase 6D.5 validator in Unity Edit Mode and Play Mode
- [ ] Manual: verify 1, 2, 6, 7, 13 and synthetic 14 visible command counts stay on one row
- [ ] Manual: verify Pawn Build commands and military Formation commands use the same size
- [ ] Manual: verify single unit, Pawn x3, mixed military and Castle selection readability
- [ ] Manual: verify Population/Wood/Gold/Food values including 0, 999, 1000+ and 15/20
- [ ] Manual: verify 1280x720 and 2560x1440 at Game View Scale 100 percent
- [ ] Manual: verify tooltip hover/clamping, disabled hover, active formation tint and no clipping
- [ ] Manual: verify command click/reject audio and UI-over-world input blocking
- [ ] Manual: recompile during Play Mode and verify no duplicate/missing pooled actions
- [ ] Confirm Unity Console has no new errors or warnings

## Phase 7A - Rule-Based Commander Foundation

Status: **PASS** - manual Play Mode validation, Phase 7A validation, Profiler
inspection and a clean Unity Console have been confirmed.

- [x] Add `ICommander`, `CommanderDecisionContext` and one Red `RuleBasedCommander`
- [x] Keep Blue human-controlled and stop Commander decisions outside `MatchState.Playing`
- [x] Add `FactionCommandService.TryMove` and `TryGather` with structured rejection results
- [x] Enforce actor ownership, match state, actor/target validity, observation and reachability
- [x] Add Red `FactionKnowledge` over own state, TeamVision and FogMemory
- [x] Rebuild visible enemy-unit observations without persistent hidden unit references
- [x] Copy building/resource last-seen memory into reference-free value snapshots
- [x] Add 0.50-second deterministic Commander tick and profiler marker
- [x] Add stable 40/35/25 economy policy with 3-worker 1/1/1 and 4-worker 2/1/1 allocations
- [x] Reuse Pawn interaction slots, gathering, cargo, Castle drop-off and resource chaining
- [x] Add temporary cooldown for rejected resource assignments
- [x] Add one stable frontier scout with Lancer/Warrior/Archer/Pawn priority and Monk exclusion
- [x] Exclude Pawn scout from economy allocation and release it after exploration completes
- [x] Add mirrored Red test resources, three Red Pawns, Red population and verified Red Pawn visuals
- [x] Keep Goblin Torch/TNT disabled and omit Gold Mine contest strategy
- [x] Add `Tools > Tiny Kingdom > Prepare Phase 7A Commander`
- [x] Add `Tools > Tiny Kingdom > Validate Phase 7A Commander`
- [x] Document legal information, anti-cheat rules and future RL sharing in `AI_SPEC.md`
- [x] Independent runtime and Editor C# compile checks: 0 errors, 0 warnings
- [x] Run `Tools > Tiny Kingdom > Prepare Phase 7A Commander` after Unity recompiles
- [x] Run `Tools > Tiny Kingdom > Validate Phase 7A Commander` in Play Mode
- [x] Manual Play Mode: verify Red begins only in Playing and Blue input remains fully functional
- [x] Manual Play Mode: verify Red 1 Food / 1 Wood / 1 Gold when three Pawns gather
- [x] Manual Play Mode: verify physical approach, work animation, cargo and Red Castle deposit
- [x] Manual Play Mode: verify depletion, same-type chaining and failed-target cooldown
- [x] Manual Play Mode: verify undiscovered resources and Blue Castle are absent from Red knowledge
- [x] Manual Play Mode: verify visible enemy units disappear from observations after vision loss
- [x] Manual Play Mode: verify hidden building/resource changes remain stale until re-scouted
- [x] Manual Play Mode: verify frontier expansion, stable destination, scout death and Pawn fallback
- [x] Manual Play Mode: verify Commander stops immediately at match end and resets on Restart
- [x] Profiler: verify 0.50-second ticks, no scene-wide search and no continuous avoidable GC
- [x] Verify no Goblins spawn or become a validator dependency and Console stays clean

## Phase 7B - Rule-Based AI Building, Population and Production

Status: **NOT COMPLETE** - implementation and independent Runtime/Editor compile
checks pass with 0 errors and 0 warnings. Unity Play Mode validator, manual tests
and Profiler inspection remain required before PASS.

- [x] Add separate `BuildingCommanderPolicy` and `ProductionCommanderPolicy`
- [x] Extend `FactionCommandService` with authoritative Build, resume, Train and Rally commands
- [x] Add structured placement, resource, population, queue and building rejection results
- [x] Add one authoritative Economy / Scout / Builder Pawn role map
- [x] Protect active Builders and Scouts from economy reassignment
- [x] Select one deterministic Builder while preserving at least two Economy Pawns
- [x] Release/recover Builder role after completion, death or invalid project state
- [x] Limit Red to one strategic construction project at a time
- [x] Search deterministic explored placement rings around Red Castle without hidden enemy queries
- [x] Reuse authoritative footprint, occupancy, terrain, reachability and resource validation
- [x] Maintain House capacity from `Cap - Used - Reserved` with a two-slot threshold
- [x] Count pending House construction and prevent duplicate House command spam
- [x] Train Red Pawns toward configurable target 6 through Castle FIFO production
- [x] Build one Barracks, one Archery and one Monastery; defer Tower strategy
- [x] Add bounded target 8 military population and one-item desired producer queues
- [x] Count living and queued units toward deterministic balanced composition
- [x] Limit Phase 7B Monk production to one
- [x] Add deterministic explored Red staging Rally for military producers only
- [x] Add retry cooldowns for rejected build and production commands
- [x] Add Red production prefabs, Red Castle queue and Red BuildingSystem wiring
- [x] Keep Goblins disabled and omit attack, defense and Gold Mine strategy
- [x] Add `Tools > Tiny Kingdom > Validate Phase 7B Commander`
- [x] Update AI, gameplay, economy and future RL contracts
- [x] Independent Runtime and Editor C# compile checks: 0 errors, 0 warnings
- [x] Add fixed-name Profiler markers for Commander stages, command dispatch and A*
- [x] Add Editor/development path-request counters split by gameplay and AI command source
- [x] Add `Tools > Tiny Kingdom > Profile Phase 7B Commander` JSON capture tool
- [x] Add an idempotent development FPS/frame-time overlay outside `RtsBottomHud`
- [x] Remove repeated AI construction reachability preflight before authoritative `TryBuild`
- [x] Rank construction/resource/drop-off interaction slots cheaply before running A*
- [x] Replace repeated Pawn Castle `FindObjectsByType` lookup with the Castle registry
- [x] Disable ordinary Pawn route diagnostic warnings by default
- [ ] Capture comparable normal-Profiler before/after frame time, marker, GC and path metrics
- [ ] Manual Play Mode: verify the FPS overlay at 1280x720, 1920x1080 and wide Game View
- [ ] Run `Tools > Tiny Kingdom > Validate Phase 7B Commander` in Play Mode
- [ ] Manual Play Mode: verify Castle grows living + queued Pawns to 6 without queue spam
- [ ] Manual Play Mode: verify new Pawns enter normal economy and respect role ownership
- [ ] Manual Play Mode: verify House trigger, no duplicate, normal payment and +5 completion bonus
- [ ] Manual Play Mode: verify one legal Barracks, Archery and Monastery with deterministic spacing
- [ ] Manual Play Mode: verify Builder travel, construction, release, death and resume recovery
- [ ] Manual Play Mode: verify population deadlock pauses production until House completes
- [ ] Manual Play Mode: verify shallow FIFO queues and bounded 8-population balanced army
- [ ] Manual Play Mode: verify Monk count remains at most one
- [ ] Manual Play Mode: verify safe military Rally and Castle Pawn Rally independence
- [ ] Manual Play Mode: verify destroyed producer/project references clear and reconstruction works
- [ ] Manual Play Mode: verify no organized military attack and no hidden Blue information use
- [ ] Manual Play Mode: verify all Blue human gameplay and Phase 7A behavior remain functional
- [ ] Profiler: verify 0.50-second think, event-driven placement search and no continuous GC/path spam
- [ ] Confirm Unity Console has no new errors or recurring AI rejection warnings

## Phase 7C.1 - Military Commander Foundation: Defense and Regroup

Status: **NOT COMPLETE** - implementation and documentation are prepared.
Runtime/Editor compilation, Play Mode validator, manual behavior tests and
Profiler comparison are still required before PASS.

- [x] Add one incremental Red `ArmyGroup` with Forming / Ready / Defending / Regrouping states
- [x] Include Warrior, Archer, Lancer and Monk; exclude Pawn, dead, stationed and active Scout units
- [x] Add `MilitaryCommanderPolicy` to the existing 0.50-second Commander pipeline
- [x] Reuse the Red Castle footprint center as base anchor and Phase 7B Rally staging cell
- [x] Configure provisional BaseDefenseRadius 9 and DefenseLeashRadius 12
- [x] Copy visible enemy combat activity into reference-free legal observations
- [x] Add deterministic defense priority, stable current target and leash/lost-vision clearing
- [x] Add authoritative `FactionCommandService.TryAttack` with ownership and current-vision validation
- [x] Use normal combat for Warrior/Archer/Lancer and Move-only support for Monk
- [x] Distribute regroup destinations around staging and deduplicate Attack/Move intent
- [x] Preserve Scout role separation, Phase 7A economy and Phase 7B build/production policies
- [x] Add fixed MilitaryPolicy, ThreatEvaluation and ArmyRegroup Profiler markers
- [x] Add optional Inspector debug data and disabled-by-default defense Gizmos
- [x] Add `Tools > Tiny Kingdom > Validate Phase 7C.1 Commander`
- [x] Document defensive scope and future RL command sharing
- [x] Independent Runtime and Editor C# compile checks: 0 errors, 0 warnings
- [ ] Run `Tools > Tiny Kingdom > Validate Phase 7C.1 Commander` in Play Mode
- [ ] Manual Play Mode: verify produced military gathers at staging and becomes Ready
- [ ] Manual Play Mode: verify no response before Blue attacker enters legitimate Red vision
- [ ] Manual Play Mode: verify one visible Warrior triggers defense and death triggers regroup
- [ ] Manual Play Mode: verify retreat beyond leash and lost vision stop strategic pursuit
- [ ] Manual Play Mode: verify building/Pawn attackers receive correct defense priority
- [ ] Manual Play Mode: verify 2-3 attackers do not cause target or command thrashing
- [ ] Manual Play Mode: verify Monk follows/supports without receiving Attack
- [ ] Manual Play Mode: verify active military Scout remains outside ArmyGroup
- [ ] Manual Play Mode: verify discovered Blue Castle never triggers offensive movement
- [ ] Manual Play Mode: verify match end/restart clear target, membership and intent caches
- [ ] Manual Play Mode: verify Blue selection, commands, economy, production and combat regressions
- [ ] Profile Commander/Military markers, GC and path requests against Phase 7B baseline
- [ ] Confirm Goblins remain disabled and Unity Console is clean

## Phase 7C.1A - Strategic Safety Hotfix

Status: **NOT COMPLETE** - implementation, documentation and independent Runtime /
Editor compilation are complete. Unity Play Mode validation and performance
comparison remain required before PASS.

- [x] Diagnose both cross-map sources: Economy resource selection and Pawn same-type chaining
- [x] Diagnose Scout frontier ranking/path persistence without risk awareness
- [x] Add reusable legal-knowledge-only `StrategicSafetyMap` with reused 40x28 buffers
- [x] Configure provisional HomeEconomicRadius 11 around Red Main Castle
- [x] Derive remembered Tower/Castle danger from recorded defense range plus 1.5 margin
- [x] Add current-visible Warrior/Archer/Lancer danger with 2.5 reaction buffer
- [x] Remove dynamic unit danger immediately when the unit leaves legal Red vision
- [x] Require Red economy targets and routes to remain known, explored, local and safe
- [x] Add safe alternate resource fallback without forcing exact 40/35/25 ratios
- [x] Pass optional AI eligibility/traversal policy through Pawn same-type resource chaining
- [x] Preserve the original human Gather and default A* APIs
- [x] Add civilian retreat and pause Builders only when their own site becomes unsafe
- [x] Allow retreat routes to escape an overlapping danger zone while keeping the destination Safe
- [x] Add Scout Exploring/Retreating state and deterministic risk/perimeter frontier ranking
- [x] Extend existing A* with optional AI traversal cost/entry policy
- [x] Add fixed StrategicSafety, SafeResourceSearch, ScoutRiskEvaluation, ScoutRetreat and CivilianRetreat markers
- [x] Add optional Inspector safety state and disabled-by-default safety Gizmos
- [x] Add `Tools > Tiny Kingdom > Validate Phase 7C.1A Strategic Safety`
- [x] Update AI, gameplay, Fog knowledge and future RL contracts
- [x] Independent Runtime and Editor C# compile checks: 0 errors, 0 warnings
- [ ] Run Phase 7C.1 and Phase 7C.1A validators in Unity Play Mode
- [ ] Manual: deplete local Red Sheep and verify no cross-map Food expedition
- [ ] Manual: verify safe Wood/Gold fallback and no command spam when local Food is unavailable
- [ ] Manual: verify known Tower memory excludes nearby resources; unknown Tower remains unknown
- [ ] Manual: verify Pawn cancels unsafe Gather and retreats from a newly visible Blue army
- [ ] Manual: verify Scout retreats on Tower/Castle/army discovery, then explores another perimeter
- [ ] Manual: verify hidden enemy-unit danger disappears and no hidden Transform is tracked
- [ ] Manual: verify Builder pauses only when its construction area becomes dangerous
- [ ] Manual: verify Blue Gather chaining, Move, Build and manual scouting remain unchanged
- [ ] Profile 0.50-second ticks, strategic markers, path request count, GC and FPS against Phase 7C.1
- [ ] Confirm no offensive orders, Goblins remain disabled and Unity Console is clean

## Building Presentation + Footprint + Command Icon Hotfix

Status: **NOT COMPLETE** - implementation, documentation and independent Runtime /
Editor compilation pass. Unity asset refresh, automated Play Mode validation and
manual presentation/regression checks remain required.

- [x] Map AttackMove to verified `Icon_05_0`
- [x] Map all Eject All command entries to verified `Icon_09_0`
- [x] Preserve Point, uncompressed, no-mipmap import for the newly used Icon_05
- [x] Scale normal Blue/Red faction building visuals to 1.7 from a ground anchor
- [x] Keep gameplay roots at scale 1.0 and preserve all explicit logical footprints
- [x] Correct Castle interaction collider from 3x2.6 to its authoritative 3x2 footprint
- [x] Keep preview art at final visual size while its border shows the logical footprint
- [x] Add Y-axis ground sorting for units/buildings without per-frame sorting work
- [x] Move health/progress/count UI, Fire, Explosion and mounted Archer anchors with the enlarged art
- [x] Preserve Gold Mine independent 1.30 presentation tuning
- [x] Add `Tools > Tiny Kingdom > Validate Building Presentation Hotfix`
- [x] Independent Runtime and Editor C# compile checks: 0 errors, 0 warnings
- [ ] Run `Prepare RTS HUD`, then the hotfix validator in Edit Mode and Play Mode
- [ ] Verify AttackMove Icon_05 and Eject All Icon_09 on Castle, Tower and Gold Mine
- [ ] Verify each building preview/final art match, bars/VFX align and no completion size pop occurs
- [ ] Verify units route only around logical footprints and pass behind upper building artwork
- [ ] Verify construction, repair, garrison, mounted Archers, rally, drop-off, Fog memory and clean Console

## Unit Scale + Resource Selection + Building Click Polish

Status: **NOT COMPLETE** - implementation, documentation and independent Runtime /
Editor compilation pass. Unity Play Mode visual and regression validation remain.

- [x] Scale animated unit, Tree and Sheep visuals to 1.3 without scaling gameplay roots
- [x] Scale Gold Stone 1-5 to 1.4 and Gold Stone 6 to 1.55 without changing gameplay roots
- [x] Keep Gold Stone 1-5 mining positions close while using extra outward clearance for Gold Stone 6
- [x] Add one navigation-clearance cell to each horizontal side of the 1.7x Main Castle art
- [x] Prevent Sheep wandering into cells currently occupied by living units
- [x] Historical moving-Sheep repath behavior implemented (superseded by Phase 6H.2 soft Sheep)
- [x] Preserve blocker exit behavior for true static occupancy; Sheep no longer creates it in Phase 6H.2
- [x] Preserve verified animation anchors across Idle, Run, Attack, Guard and work frames
- [x] Raise the unscaled unit health-bar anchor from 0.78 to 1.01
- [x] Scale unit selection-ring geometry and ground offset by 1.3 to restore foot alignment
- [x] Add green ground-selection rings for Tree, Gold Stone, Sheep and Gold Mine
- [x] Hide resource rings when selection clears, becomes invalid or leaves presentation
- [x] Add a ground-anchored building click pulse with mild 0.94/1.035 vertical deformation
- [x] Keep building click pulse independent from HP, construction and damage timing
- [x] Reuse the verified Hammer animation for repair and pulse the building on each successful 0.35-second repair impact
- [x] Add `Tools > Tiny Kingdom > Validate Selection And Unit Scale Polish`
- [x] Independent Runtime and Editor C# compile checks: 0 errors, 0 warnings
- [ ] Run the new validator in Edit Mode and Play Mode
- [ ] Verify Blue/Red Pawn, Warrior, Archer, Lancer and Monk visuals at 1.3 across all states
- [ ] Verify Tree and Sheep at 1.3, Gold Stone 1-5 at 1.4 and Gold Stone 6 at 1.55 without collider/occupancy changes
- [ ] Verify selection rings for live and depleted-selectable resources under Fog transitions
- [ ] Verify Pawns remain outside all six Gold Stone visuals while mining from cardinal and diagonal slots
- [ ] Verify Gold Stone 1-5 Pawns stand close, Gold Stone 6 Pawns stay outside, and all mining animations face the node
- [ ] Damage each constructible building and verify every successful Hammer repair impact triggers one mild pulse
- [ ] Verify units route around both visible Main Castle side edges while drop-off/garrison entry still works
- [x] Superseded by Phase 6H.2: units now pass through Sheep instead of detouring
- [ ] Verify clustered units pass through Sheep without pathfinding or Console spam
- [ ] Verify repeated building clicks restart one pulse and always recover to scale 1.7
- [ ] Verify movement, combat, gathering, construction, garrison, cursor and clean Console regressions

## Phase 7C.2 - Offensive Strategy, Strategic Targeting and AttackMove

Status: **NOT COMPLETE** - implementation, documentation and independent Runtime /
Editor compilation pass with 0 errors and 0 warnings. Unity Play Mode validation
and Profiler comparison remain required before PASS.

- [x] Extend ArmyGroup states with PreparingAttack, Advancing and Assaulting
- [x] Configure provisional minimum offensive military population 6 and 75% assembly rule
- [x] Snapshot operation membership so new production remains at Rally staging
- [x] Select deterministic strategic objectives only from legal building knowledge
- [x] Prioritize Tower, production, other useful building, then Castle with risk/distance/EntityId ties
- [x] Keep one stable objective and ignore routine new discoveries during an active operation
- [x] Treat hidden building memory as an AttackMove destination only
- [x] Add current-vision-validated `TryAttackBuilding` and ownership-validated `TryAttackMove`
- [x] Add risk-tolerant Army traversal costs without blocking assaults on Tower/Castle
- [x] Distribute group advance destinations and deduplicate per-unit command intent
- [x] Keep Monk support-only and active Scout outside ArmyGroup
- [x] Keep newly produced military at staging until the next offensive
- [x] Preserve high-priority Phase 7C.1 base-defense interruption
- [x] Clear stale/destroyed objectives after legal re-observation and regroup survivors
- [x] Add bounded path-failure handling and provisional 3-second reevaluation delay
- [x] Reset objective, operation membership, retries and intent caches with Commander reset
- [x] Add fixed OffensiveEvaluation, StrategicTargetSelection, OffensiveAdvance and OffensiveRegroup markers
- [x] Add optional Inspector diagnostics and disabled-by-default legal target Gizmo
- [x] Add `Tools > Tiny Kingdom > Validate Phase 7C.2 Commander`
- [x] Update AI, gameplay, Fog and future RL contracts
- [x] Independent Runtime and Editor C# compile checks: 0 errors, 0 warnings
- [ ] Run `Tools > Tiny Kingdom > Validate Phase 7C.2 Commander` in Unity Play Mode
- [ ] Manual: ready army with no known building stays staged while Scout continues
- [ ] Manual: remembered Barracks guides AttackMove but cannot receive hidden direct Attack
- [ ] Manual: visible objective is attacked through normal Warrior/Archer/Lancer combat
- [ ] Manual: stale memory clears only after the area is re-revealed
- [ ] Manual: current objective remains stable when Scout discovers another building
- [ ] Manual: group cohesion, Monk support and no single-unit reinforcement stream
- [ ] Manual: serious Red-base threat interrupts offense and survivors regroup
- [ ] Manual: objective destruction, Army wipe and bounded unreachable-target handling
- [ ] Manual: unknown Castle is never targeted; legitimately discovered Castle may be targeted
- [ ] Manual: Scout/Pawn strategic safety, Fog anti-wallhack and no Goblin regression
- [ ] Manual: Blue human controls, 1.7x building presentation/navigation and Match End/Restart
- [ ] Profile 0.50-second Commander tick, offensive markers, GC and path requests against Phase 7C.1
- [ ] Confirm Unity Console has no new errors or warnings

## Phase 7C.3 - Combat Strength, Retreat and Recovery

Status: **NOT COMPLETE** - implementation, documentation and independent Runtime /
Editor compilation pass with 0 errors and 0 warnings. Unity Play Mode validation
and Profiler comparison remain required before PASS.

- [x] Add explicit Retreating and Recovering ArmyGroup states
- [x] Add configurable health-weighted Warrior/Archer/Lancer and Monk support strength
- [x] Restrict exact enemy strength to currently visible local knowledge snapshots
- [x] Restrict visible Tower/Castle strength to the nine-unit campaign battle radius
- [x] Capture stable campaign population, member, strength and average-HP baselines
- [x] Keep newly produced units out of campaign survivor calculations
- [x] Add immediate 0.45 remaining-force retreat and two-sample HP/enemy hysteresis
- [x] Add generic CombatDirective.Retreat and validated TryRetreatMove authority
- [x] Cancel AttackMove, proactive acquisition, retaliation and chase during retreat
- [x] Distribute retreat destinations around the existing Rally staging point
- [x] Deduplicate retreat movement and bound straggler waiting to 12 seconds
- [x] Add achievable recovery without requiring full HP, regeneration or Monk
- [x] Add a 10-second no-healing recovery ceiling to prevent permanent deadlock
- [x] Keep economy, production and Scout independent during retreat/recovery
- [x] Add 12-second failed-objective cooldown with legal alternative selection
- [x] Preserve base-defense priority with withdrawal/recovery hysteresis
- [x] Add fixed combat-strength, retreat and recovery Profiler markers
- [x] Add optional Inspector debug fields and legal local battle-radius Gizmo
- [x] Add `Tools > Tiny Kingdom > Validate Phase 7C.3 Commander`
- [x] Update AI, gameplay, Fog and future RL contracts
- [x] Independent Runtime and Editor C# compile checks: 0 errors, 0 warnings
- [ ] Run `Tools > Tiny Kingdom > Validate Phase 7C.3 Commander` in Unity Play Mode
- [ ] Manual: winning and small-disadvantage battles continue without panic
- [ ] Manual: overwhelming visible local force triggers only after two evaluations
- [ ] Manual: heavy casualties and very low average HP trigger retreat correctly
- [ ] Manual: one injured unit or Monk loss alone does not retreat the Army
- [ ] Manual: offensive AttackMove clears and survivors do not reacquire/chase
- [ ] Manual: retreat destinations distribute and one straggler cannot deadlock recovery
- [ ] Manual: hidden enemies immediately leave exact strength and remote Scout contacts do not count
- [ ] Manual: hidden remembered Tower affects strategic risk without live HP/garrison reads
- [ ] Manual: recovery succeeds with Monk and through reinforcement without Monk
- [ ] Manual: new production remains staged while campaign survivors retreat
- [ ] Manual: failed objective cooldown permits alternatives and later expires
- [ ] Manual: defense interrupts withdrawal/recovery without command oscillation
- [ ] Manual: Army wipe, Match End and Restart clear all temporary state
- [ ] Manual: successful Phase 7C.2 offense and Strategic Safety remain functional
- [ ] Manual: Blue controls, economy, construction, production, combat and Fog regressions
- [ ] Profile Commander/strength/retreat/recovery markers, path requests, GC and FPS
- [ ] Confirm Unity Console has no new errors or warnings

## Phase 7D - Full Rule-Based AI Integration, Balancing and Match Regression

Status: **NOT COMPLETE** - integration hardening, progress diagnostics, validator,
documentation and independent Runtime / Editor compilation are complete. Full-match
Play Mode scenarios and actual Profiler validation remain mandatory before PASS.

- [x] Audit policy order and preserve one legal FactionKnowledge/FactionCommandService path
- [x] Preserve exclusive Pawn Economy/Scout/Builder ownership and Scout/Army separation
- [x] Use configured 75% normal Army staging instead of a 100% straggler-sensitive gate
- [x] Bound PreparingAttack to 10 seconds with a deterministic 60% fallback
- [x] Fail and cool down an objective cleanly when timeout assembly remains below 60%
- [x] Add stable Scout frontier-exhaustion standby and two-second blocked-frontier retry
- [x] Add low-frequency Commander progress timestamps without gameplay overrides
- [x] Keep optional 25-second stall warning disabled by default and rate limited
- [x] Record final provisional Rule-Based tuning and intentional limitations
- [x] Add `Tools > Tiny Kingdom > Validate Phase 7D Full Match`
- [x] Preserve anti-wallhack, Strategic Safety, deterministic ordering and shared commands
- [x] Preserve all core gameplay stats, audio pause, disabled Goblins and current UI
- [x] Independent Runtime and Editor C# compile checks: 0 errors, 0 warnings
- [ ] Refresh Unity assets and run `Validate Phase 7D Full Match` in Play Mode
- [ ] Scenario: passive Blue; Red economy, infrastructure, Army, Scout and offense progress
- [ ] Scenario: early Blue pressure; defense resolves and normal progression resumes
- [ ] Scenario: Red successful offense; target resolves and survivors regroup
- [ ] Scenario: failed offense; Retreat -> Recovery -> reinforcement -> later campaign
- [ ] Scenario: deplete local resources; workers switch safely or remain legitimately Idle
- [ ] Scenario: kill Scout and Builder; roles/projects recover without duplicates
- [ ] Scenario: destroy Barracks/Archery; core infrastructure is rebuilt when affordable
- [ ] Scenario: wipe Red Army; production forms a new Army and strategy resumes
- [ ] Scenario: undiscovered Blue Castle is never targeted, then becomes legal after scouting
- [ ] Scenario: stale hidden building memory clears only after legal re-observation
- [ ] Scenario: Red destroys Blue Castle and normal MatchManager result stops all new AI commands
- [ ] Scenario: Blue destroys Red Castle and Commander stops without exception
- [ ] Restart during economy, construction, offense, retreat, recovery and after Match End
- [ ] Run at least three long/full sessions; recommended 10-15 minutes where practical
- [ ] Profile early, mid, active combat and late match FPS / GC Alloc / Commander cost
- [ ] Confirm no repeated identical A*, runtime scene search, warning spam or registry growth
- [ ] Verify Blue controls, Fog, buildings, HUD and Console regressions

### Phase 8A - RL Environment Foundation (roadmap only)

- [ ] Begin only after Phase 7D full-match and Profiler gates pass
- [ ] Reuse FactionKnowledge observations and FactionCommandService actions
- [ ] Define environment reset, legal observation tensor, action mask and baseline metrics
- [ ] Do not grant omniscience, low-level unit control or Rule-Based-only cheats

## Gameplay SFX Foundation / Initial Audio Integration

Status: **NOT COMPLETE** - implementation and data setup are present. Unity asset
refresh, Edit/Play Mode validator runs and manual listening/Fog regression tests
remain required before PASS. This is not the full Phase 6E audio phase.

- [x] Add a serialized GameplayAudioCatalog with six active initial clip references
- [x] Add a persistent GameplayAudioService with a 12-source initial reusable pool
- [x] Add per-event volume, playback cap and optional subtle pitch tuning
- [x] Add SfxVolume with default 1.0 for future settings/mixer integration
- [x] Trigger Warrior/Lancer only after successful melee damage
- [x] Trigger Archer at actual projectile creation
- [x] Trigger Monk only after positive HP restoration
- [x] Trigger mining/chopping only after positive resource transfer
- [x] Preserve the requested removal of Sheep Hurt playback
- [x] Suppress hidden enemy/neutral world SFX through current TeamVision visibility
- [x] Stop and reset pooled sources on Match End and scene reload
- [x] Preserve one existing Main Camera AudioListener and add no listener
- [x] Add `Tools > Tiny Kingdom > Prepare Gameplay Audio`
- [x] Add `Tools > Tiny Kingdom > Validate Gameplay Audio`
- [x] Keep provisional Mining 0.20 and Chopping 0.65 volume tuning
- [x] Skip the silent first 3.0 seconds of the supplied Chopping clip
- [x] Use Main Camera centered linear distance attenuation for world SFX
- [ ] Refresh assets and run Prepare/Validate in Unity Edit Mode
- [ ] Run Validate again in Play Mode and confirm one service/listener and valid pool
- [ ] Manually verify each of the seven action triggers and no travel/command sound
- [ ] Tune volume/playback caps after listening to the supplied long action clips
- [ ] Verify hidden Red combat and hidden Sheep damage produce no audio leak
- [ ] Verify camera distance attenuation, multi-unit concurrency and economy load
- [ ] Verify Match End/Restart, Blue controls, Red AI, Fog and clean Console
- [ ] Add Pawn Food, generic impacts/death, ambience and music later

## Phase 6E.2 - Extended Gameplay SFX, Building Audio and UI Notifications

Status: **NOT COMPLETE** - runtime/editor code and serialized mappings are implemented,
and both assemblies compile with zero errors. Unity Edit/Play Mode validator runs,
manual listening, Fog, overlap and restart validation remain before PASS.

- [x] Verify Castle, House, Barracks, Archery, Monastery and Tower selection clips
- [x] Record that no Gold Mine selection clip is supplied
- [x] Add successful building-selection acknowledgement and allow explicit reselect
- [x] Align building Fire VFX/audio threshold to below 60% HP
- [x] Add one Fog-aware positional Fire loop handle per observable damaged building
- [x] Stop Fire at 60%+, on destruction, Match End, loss of vision and scene reload
- [x] Add one observable positional building-destruction sound
- [x] Add 2D building-plan sound only after successful Human placement
- [x] Add two Pawn hammer variations for real construction cycles/positive repairs
- [x] Add accepted/rejected command-button feedback from authoritative command result
- [x] Keep visually disabled command buttons non-executable while allowing reject audio
- [x] Add Human production-finished notification after successful spawn only
- [x] Add once-per-match Victory/Defeat audio from `MatchManager.MatchEnded`
- [x] Replace per-event concurrency/cooldown suppression with expandable source pooling
- [x] Permit simultaneous identical combat, work, explosion and queue SFX
- [x] Preserve viewport attenuation and TeamVision anti-wallhack rules for world audio
- [x] Add separate 2D path for direct player feedback and result audio
- [x] Add Phase 6E.2 setup/validator and debug source counters
- [x] Add `AUDIO_SPEC.md` and update gameplay/asset documentation
- [x] Compile Runtime and Editor assemblies with zero errors/warnings
- [ ] Run `Prepare Gameplay Audio` after Unity finishes refreshing assets
- [ ] Run Phase 6E.2 validator in Edit Mode
- [ ] Run Phase 6E.2 validator in Play Mode and record pool/loop counters
- [ ] Manually test all building selection mappings and repeat-selection behavior
- [ ] Manually test 59%/60% Fire lifecycle, repair, multiple fires and Fog suspension
- [ ] Manually test visible/hidden destruction and simultaneous explosions
- [ ] Manually test valid/invalid placement and build command two-sound flow
- [ ] Manually test two hammer variations, multiple builders and positive repair impacts
- [ ] Manually test accepted/rejected/disabled command buttons and mutual exclusion
- [ ] Manually test own queue completion and hidden Red production anti-leak
- [ ] Manually test Victory, Defeat, once-per-match guard and Restart reset
- [ ] Stress simultaneous combat/economy audio and inspect Audio CPU/GC/pool growth
- [ ] Confirm the Unity Console remains clean
- [ ] Decide separately whether to integrate the supplied `attack_theme.mp3`

## Phase 6F.1 - Elevation-Aware World Sorting, Animated Bushes and Tree Chop Feedback

Status: **PASS** - implementation and projected-depth hotfix compile cleanly;
manual Play Mode visual/regression validation was confirmed on 2026-10-02.

- [x] Make logical `GridCell.HeightLevel` authoritative for projected world depth
- [x] Replace absolute elevation bands with one global projected ground-depth domain
- [x] Project each ground contact by one visual cell per logical elevation level
- [x] Dynamically update all unit types and wandering Sheep across connectors
- [x] Interpolate presentation elevation continuously across reciprocal connectors
- [x] Resolve Trees, Gold, buildings, mine and decorative props from their ground cell
- [x] Keep full terrain Tilemaps background-only and in Chunk mode
- [x] Give southern cliff faces and official stair halves projected foreground copies
- [x] Keep world orders below existing Fog/presentation overlays
- [x] Verify Bushe1..4 paths, Aseprite tags, eight-frame sequences and 100 ms timing
- [x] Classify Bushe1/3 as LARGE (1.50) and Bushe2/4 as SMALL (1.25)
- [x] Preserve Point, 100 PPU, uncompressed, no-mipmap, Clamp bush imports
- [x] Animate bushes through one shared frame scheduler with deterministic phases
- [x] Trigger one 3.5 degree / 0.22 second Tree wobble per successful Wood take
- [x] Refresh rather than queue repeated impacts and restore exact rotation safely
- [x] Preserve roots, hit areas, navigation, Fog authority and chopping SFX
- [x] Add `Tools > Tiny Kingdom > Validate Phase 6F.1 World Presentation`
- [ ] Run the Phase 6F.1 validator in Edit and Play Mode when Unity licensing is available
- [x] Reproduce lower Sheep vs higher Tree and lower Pawn vs upper cliff cases
- [x] Verify same-level Pawn/Sheep vs Tree/Bush front/back Y sorting
- [x] Verify level 1 Pawn, Sheep and building can cover level 2/3 background props
- [x] Verify a genuinely foreground higher-level object still covers a lower-level object
- [x] Verify level 1/2/3 objects can interleave by projected depth without level blocks
- [x] Verify projected southern cliff faces occlude only at the intended contact depth
- [x] Move Pawn, Warrior, Archer, Lancer and Monk through official stairs without flicker
- [x] Verify Sheep, Tree, Gold, Castle and all constructible buildings across valid levels
- [x] Observe all four Bush loops for sharpness, frame order, stable pivots and desynchronization
- [x] Verify single/multiple Pawn chop wobble, stable Hand cursor and safe final depletion
- [x] Confirm Fog, health bars, selection rings, Console, Profiler and GC regressions

## Phase 6G.1 - Role-Based Army Movement and Frontline Ordering

Status: **NOT COMPLETE** - implementation and automated validator are present;
directional Play Mode, AI regression, Fog regression and Console validation remain.

- [x] Centralize Warrior, Lancer, Archer, Monk and Pawn formation priority
- [x] Derive forward/right axes from group centroid toward the Human command point
- [x] Generate compact role rows with configurable one-cell spacing
- [x] Collapse absent roles without reserving empty rows
- [x] Assign same-role units by deterministic nearest-pair matching and EntityId ties
- [x] Correct desired slots through existing bounded logical-grid distribution
- [x] Preserve unique, walkable, unoccupied, same-height destination cells
- [x] Keep one selected unit on the previous command path without formation offsets
- [x] Apply the planner to Human Move and eligible ground AttackMove commands
- [x] Preserve direct-target Attack, A*, UnitSeparation, combat and unit speeds
- [x] Leave FactionCommandService and RuleBasedCommander call sites unchanged
- [x] Add `Tools > Tiny Kingdom > Validate Phase 6G.1 Role Formation`
- [ ] Run the Phase 6G.1 validator in Unity Edit Mode
- [ ] Test right, left, up, down and four diagonal Human group commands
- [ ] Test mixed roles, ranged-only, Pawn-inclusive and same-role groups
- [ ] Test obstacle correction, map edge, choke passage and rapid recommand
- [ ] Verify AttackMove acquisition/resume and direct target Attack regressions
- [ ] Verify Red RuleBasedCommander, Fog, performance and clean Console

## Phase 6G.2 - Player Formation Commands and Formation Shapes

Status: **NOT COMPLETE** - implementation and automated validator are present;
Unity Edit Mode validation and the required Play Mode regression matrix remain.

- [x] Define exactly Compact, Wedge, Loose and Line; omit Column/SingleColumn
- [x] Keep Compact as the match-start default and persist the Human preference
- [x] Reuse one Phase 6G.1 slot, role assignment and logical-grid correction path
- [x] Generate adaptive Compact rows at the existing one-cell spacing
- [x] Generate widening Wedge rows with centered partial final rows
- [x] Generate Loose from Compact with a provisional 1.65 spacing multiplier
- [x] Generate Line as exactly one centered lateral rank for even and odd counts
- [x] Assign Compact/Wedge/Loose roles front-to-rear in canonical priority
- [x] Assign Line roles center-to-wing in canonical priority
- [x] Restrict selectable formations to Warrior, Lancer, Archer and Monk
- [x] Exclude Pawn from formation visibility counts, slots, centroid and reform
- [x] Preserve Pawn gather/build/repair jobs when a mixed selection reforms
- [x] Partition normal mixed Move into military formation and generic Pawn destinations
- [x] Keep Pawn destinations outside cells reserved by the military formation
- [x] Rotate all shapes with the shared forward/lateral basis
- [x] Use the selected shape for Human Move and initial ground AttackMove layout
- [x] Reform the current group in place using last meaningful facing
- [x] Preserve single-unit and direct-target Attack behavior
- [x] Add four contextual Command Panel buttons with procedural verified glyphs
- [x] Add tooltips, active selection tint and existing click/reject audio handling
- [x] Preserve HUD hierarchy across domain reload with fourteen reusable actions
- [x] Add `Tools > Tiny Kingdom > Validate Phase 6G.2 Formation Commands`
- [x] Extend validator with military eligibility, mixed slots and centroid isolation
- [ ] Run the Phase 6G.2 validator in Unity Edit Mode
- [ ] Verify buttons hidden for Pawn-only and one-military-plus-Pawn selections
- [ ] Verify buttons visible for two military and mixed two-military-plus-Pawn selections
- [ ] Test Compact, Wedge, Loose and Line reform while idle and while moving
- [ ] Test up/down/left/right and four diagonal movement for every shape
- [ ] Test Line even, odd, two-unit, mixed-role and 20+ unit groups
- [ ] Verify mixed reform never moves Pawns or cancels gather/build/repair jobs
- [ ] Verify normal mixed Move still moves Pawns outside military formation slots
- [ ] Test absent military roles, map edges, obstacles, choke passages and rapid recommands
- [ ] Verify Move, AttackMove acquisition/resume and direct target Attack regressions
- [ ] Verify Phase 6D HUD/tooltips, Phase 6E audio and Phase 6F world sorting regressions
- [ ] Verify RuleBasedCommander, Fog, Console and Profiler/GC regressions

## Phase 6H.1 - Economy and Worker Logistics

Status: **PASS** - implementation compiles cleanly and the user confirmed the
Phase 6H.1 Unity Play Mode validation is successful.

- [x] Generalize Castle/Tower deposits through `IResourceDropOff` and one registry
- [x] Accept Food, Wood and Gold at living completed friendly Towers
- [x] Rank drop-offs by squared distance and stable EntityId before route checks
- [x] Recalculate when a selected drop-off is destroyed, invalid or unreachable
- [x] Change authoritative House cost from 100 Wood to 80 Wood
- [x] Give every completed House at most one generated Sheep
- [x] Spawn House Sheep outside the footprint on bounded valid walkable rings
- [x] Respawn a depleted House Sheep after 90 gameplay seconds
- [x] Cancel future respawn on House destruction while preserving a living Sheep
- [x] Redirect slot-overflow Pawns to a same-type visible node with free capacity
- [x] Continue depleted jobs across visible and legally remembered same-type resources
- [x] Exclude unexplored resources and defer hidden live-state resolution until visible
- [x] Preserve stable reservations until invalidation or explicit manual override
- [x] Change authoritative Pawn carry capacity from 10 to 20
- [x] Change authoritative gather amount from 1 to 2 per successful work tick
- [x] Preserve the 19-to-20 cap edge without removing the unused resource unit
- [x] Pass AI safety filtering to visible and remembered continuation positions
- [x] Add `Tools > Tiny Kingdom > Validate Phase 6H.1 Economy & Worker Logistics`
- [x] Compile temporary Runtime and Editor checks with 0 errors and 0 warnings
- [x] Refresh scripts in the active Unity Editor and confirm Console compilation
- [x] Run the Phase 6H.1 validator in Edit Mode
- [x] Run the Phase 6H.1 validator in Play Mode
- [x] Test Tower versus Castle nearest drop-off and Tower destruction fallback
- [x] Build House A/B/C, verify 80 Wood and one Sheep outside each footprint
- [x] Deplete House Sheep, verify no duplicate and one replacement after 90 seconds
- [x] Destroy House before/after Sheep depletion and verify ownership cleanup rules
- [x] Saturate one Tree/Sheep/Gold node with a Pawn group and verify redistribution
- [x] Deplete each resource type and verify same-type visible/memory continuation
- [x] Verify unexplored resources are never automatically targeted
- [x] Verify carry 20, +2 ticks, 19-to-20 edge and no negative remaining amount
- [x] Verify Blue controls, Red AI, production, combat, Fog, construction, garrison,
  rally, navigation, Console and Profiler/GC regressions
- [x] Phase 6H.2 was explicitly started after Phase 6H.1 validation

## Phase 6H.2 - Navigation and Sheep Interaction Polish

Status: **PASS** - implementation compiles cleanly and the user confirmed the
Phase 6H.2 Unity Play Mode validation is successful.

- [x] Preserve reciprocal lower-to-upper and upper-to-lower stair edges
- [x] Consume connector endpoints one at a time during continuous movement
- [x] Validate authored connector traversal without ambiguous shared-corner sampling
- [x] Suppress local separation only during the short connector waypoint traversal
- [x] Add a 0.75-second connector no-progress watchdog with throttled repath
- [x] Preserve the 0.35-second blocked-route repath cooldown after successful rebuild
- [x] Remove Sheep from hard logical navigation occupancy and A* blocking
- [x] Keep Sheep gameplay and selection colliders as nonblocking triggers
- [x] Exclude only Sheep from resource-based placement rejection
- [x] Find distinct Sheep escape cells with a bounded four-ring search
- [x] Move all affected Sheep after successful building placement
- [x] Preserve authoritative building footprint blocking and Sheep Food semantics
- [x] Add `Tools > Tiny Kingdom > Validate Phase 6H.2 Navigation & Sheep`
- [x] Run the Phase 6H.2 validator in Unity Edit Mode
- [x] Repeat Pawn and Warrior ascent/descent on every intended connector
- [x] Move 5+ units through one stair and test opposing directions
- [x] Verify units path directly through stationary and moving Sheep
- [x] Verify building preview remains legal over Sheep on otherwise valid terrain
- [x] Place buildings over one/multiple Sheep and verify distinct safe evacuation
- [x] Verify Sheep hover/select/Gather, building blocking and clean Console
- [x] Verify combat, formation, Fog, AI, economy values and House respawn regressions
- [x] Phase 6H.3 has not begun
