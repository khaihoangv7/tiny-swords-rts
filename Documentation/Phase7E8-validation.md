# Phase 7E.8 gameplay stabilization validation

Date: 2026-10-28. Branch: `integration/full-game` at `1c72417` when testing began.

This report records observed outcomes only. The working tree already contained many unrelated local edits; they were preserved. No balance value, branch, commit, stash, or remote was changed for this phase.

## Phase 7E.7 gate

The former `Red Pawn did not claim the mine` failure did not reproduce in the Unity GUI Play Mode fixture. The Red logistics scenario verified mine assignment, Castle deposit exactly once, and a second trip by the same Pawn. The blocked-entry, Blue claim, and unreachable-entry recovery scenarios also passed. Headless batch execution did not create a playable match in this environment, so those checks were run in GUI Play Mode.

On 2026-10-29, the isolated copy at `D:\TinyKingdomRL-Phase7E8-Headless` reproduced the batch startup stall. Unity Search startup indexing threw an Editor-only `ArgumentOutOfRangeException` and paused the Editor; a later forced assembly reload after `EnteredPlayMode` erased the fixture's runtime setup. The validator now resumes only that recognized startup pause (at most three times), holds assembly reload until the test finishes, and still fails on unrelated errors. The actual Red logistics Play Mode scenario then passed headlessly: Red assigned a Pawn, mined, deposited once at its Castle, and repeated the trip (`Logs/Phase7E8-Headless-FinalHarness-3.log`). This is a tested batch fixture recovery, not a claim that Unity's Search indexing defect is fixed.

## Structural and Play Mode checks

| Check | Observed result | Limit |
| --- | --- | --- |
| Phase 7E.8 structural regression suite | 14/14 passed in Edit Mode | Does not establish full-match behavior |
| Phase 7E.7 Red gold logistics | Passed in GUI Play Mode | Focused fixture clears nearby Goblin guards |
| Phase 7E.7 blocked mine entry | Passed in GUI Play Mode | Focused fixture |
| Phase 7E.7 Blue mine claim | Passed in GUI Play Mode | Did not assert final Blue Castle deposit |
| Phase 7E.7 unreachable recovery | Passed in GUI Play Mode | Focused fixture |
| Phase 7E.7 Red gold logistics, headless | Passed in isolated `-batchmode -nographics` Play Mode | Same focused fixture; Editor Search startup errors were classified separately |
| Phase 7E.8 Blue contests a Red-owned mine | Passed in isolated headless Play Mode | Blue Warrior ejects Red miner; Blue Pawn claims and hauls to Blue Castle; fixture clears Goblins |
| Phase 7E.8 Red recaptures a Blue-owned mine | Passed in isolated headless Play Mode | Red military ejects Blue miner and Commander assigns Red miner; fixture supplies legally revealed route and a ready force |
| Phase 7E.8 active Blue assault and mining recovery | Passed in isolated headless Play Mode | Both factions deal damage; six ready Red defenders repel two Blue attackers; the mine yields more Gold after combat; Blue attack orders are fixture-driven |
| Phase 6C Match Flow validation | Passed during isolated Play Mode setup | Checks result mapping and presentation references; does not click Restart |
| Runtime building-site destruction occupancy | Passed during isolated Play Mode setup | Destroying a damaged construction site releases its navigation cells; completed building destruction was not separately exercised |
| Unity runtime and Editor script compilation | Both succeeded with zero warnings and errors in the final `dotnet build` checks | Unity reported transient asset `.meta` write errors during an earlier import; subsequent Play Mode scenarios ran |

The Edit Mode suite contains AI 7E.1–7E.6, 6H.1 worker economy, 6H.2 stairs/sheep, elevation vision, Castle/Tower defense, RTS HUD, and gameplay audio. Its 6H.2 occupancy check now invokes the explicit release path because `DestroyImmediate` in Edit Mode does not dispatch the runtime `OnDestroy` callback. The additional Play Mode building-site destruction check exercises the runtime callback.

## Full-match measurements

The test uses the actual `Map` scene and leaves the Blue player idle. It verifies Red AI development and Match End, but cannot measure competitive balance against an active human Blue player. `Phase7E8MatchCapture` records stockpile increases, Gold deposits at each Main Castle, sampled worker/army/building counts, mine state, strategic intent, path requests, AI command attempts, runtime errors, and final match result. Gross stockpile increases can include refunds and should not be interpreted as pure harvesting without event-level attribution.

| Speed | Result | Game time | Real time | Red gross Food / Wood / Gold gains | Red Castle Gold deposits | Runtime errors | Evidence |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| x10 | RedVictory | 1,176 s | 122 s | 1,320 / 925 / 2,900 | 88 trips, 2,900 Gold | 0 during capture | `Logs/Phase7E8Match-x10-20261028-003617.json` |
| x5 | RedVictory | 770 s | 153 s | 1,320 / 912 / 2,670 | 76 trips, 2,670 Gold | 0 during capture | `Logs/Phase7E8Match-x5-20261028-003942.json` |
| x2 | RedVictory | 790 s | 395 s | 1,420 / 890 / 2,900 | 89 trips, 2,900 Gold | 0 | `D:\TinyKingdomRL-Phase7E8-Headless\Logs\Phase7E8Match-x2-20261030-060659.json` |
| x1 | RedVictory | 1,105 s | 1,105 s | 2,360 / 1,645 / 2,900 | 90 trips, 2,900 Gold | 0 | `D:\TinyKingdomRL-Phase7E8-Headless\Logs\Phase7E8Match-x1-20261029-165006.json` |

In the x10 match, the mine changed from Neutral to Red and then Depleted. Its reserve fell from 2,000 to zero by 760 game seconds. The sampled Red miner count peaked at three, and the final Red army had seven military units. Red Castle Gold deposits equaled the Red Gold stockpile increase during the capture. The sampled maximum path request rate was approximately 33 per real second over a five-second window; whole-capture totals were 1,824 path requests and 863 command attempts. These rates alone do not identify an infinite retry loop.

The x5 match also changed the mine from Neutral to Red and then Depleted, at approximately 556 game seconds. The final Red army had six military units. Red Castle Gold deposits again equaled the Red Gold stockpile increase. Its capture totaled 1,320 path requests and 726 command attempts. The x5 victory occurred 406 game seconds sooner than the x10 victory; one run per speed cannot establish the cause of this difference.

The isolated headless x1 full match ended in RedVictory after 1,105 game seconds and 1,105 real seconds with no runtime errors or warnings. Red completed six buildings, trained additional Pawns and combat units, and made 90 Castle Gold deposits. The capture counted 978 path requests and 490 AI command attempts. Mean frame time was 1.05 ms in headless mode, the worst frame was 37.9 ms, three frames exceeded 33 ms, and none exceeded 100 ms. Headless frame time does not represent graphical Game View FPS.

The isolated headless x2 replay ended in RedVictory after 790 game seconds and 395 real seconds with no runtime errors or warnings. Red made 89 Castle Gold deposits, 704 path requests and 443 AI command attempts. Mean frame time was 1.42 ms, worst frame 30.7 ms, and no frame exceeded 33 ms. The earlier x2 run was interrupted at 714 game seconds; this complete replay supersedes it.

The Game View showed roughly 60 FPS early in the first x10 run, then 14–24 FPS around 13–17 game minutes, plus one 543 ms frame. A later 30.07-second capture around game time 15:49–17:00 recorded 1,527 frames in `Logs/Phase7BPerformance.json`. One 18.8-second first frame occurred while the Editor menu/profiler interaction was starting, with zero path requests, AI commands and GC allocation, so it cannot be attributed to gameplay. Excluding that Editor stall, mean frame time was about 18.5 ms (54 FPS); the earlier sustained 14–24 FPS did not recur in this window. The largest later recorded spike was 50.3 ms, with no path request or AI command in that frame. The capture therefore does not establish the cause of the earlier slowdown.

The same profile measured `CommanderTick` on 90 active frames at 1.21 ms average and 9.88 ms maximum; Fog memory refresh at 1.00/1.44 ms; pathfinding at 0.74/7.15 ms on 47 active frames; and audio playback at 0.08/1.65 ms. It counted 139 path requests over 30.07 real seconds (4.62/s), a maximum of 17 in one frame, and at most two AI command attempts in one frame. Average managed allocation was about 16.6 KB/frame, with a 592 KB maximum. The active audio source count reached 51; whether audio mixing or rendering contributed to the earlier FPS drop remains unmeasured. These are game marker measurements, not a complete CPU/GPU frame breakdown. GUI Play Mode startup also emitted 32 warnings about runtime scripts on generated stair/cliff objects before capture; no new runtime warnings or exceptions occurred during the original x10 capture.

On 2026-10-29, the original Unity Editor Game View was restarted from Edit Mode, set to x10 using the in-game Settings control, and observed through the earlier problematic game-time interval. It displayed about 59 FPS at 1:49, 62 FPS at 9:51, and 56 FPS at 17:38, then reached the Blue defeat result while Blue was idle. This repeat did not reproduce the previous 14–24 FPS period. These are HUD spot readings rather than a continuous CPU/GPU trace, so the original slowdown's cause remains undetermined. Play Mode was stopped afterward.

## Isolated headless replay and contested-mine checks

Two additional full matches ran in the isolated headless project at true x10. Both ended in RedVictory with no gameplay errors: 827.4 game seconds in 82.7 real seconds (`Logs/Phase7E8-HeadlessMatch-x10-20261029-154639.json`) and 960.2 game seconds in 95.97 real seconds (`Logs/Phase7E8-HeadlessMatch-x10-20261029-154931.json`). Mean frame times were 7.67 and 7.05 ms; worst frames were 33.5 and 25.4 ms. The first run had one frame above 33 ms, the second had none, and neither had a frame above 100 ms. The second recorded 749 path requests and 323 AI command attempts. Both exercised actual Red mine mining/depletion and victory, with idle Blue. Headless results cannot establish GPU, UI, or audio rendering performance in the graphical Editor.

A 30.008-second x10 headless profile (`Logs/Phase7E8-Headless-x10Profiler.json`) recorded 3,990 frames, 7.52 ms mean and 25.4 ms worst frame. `CommanderTick` averaged 0.52 ms and peaked at 6.10 ms on 552 active frames; TeamVision recompute averaged 0.84 ms and peaked at 1.70 ms; Fog memory refresh averaged 0.57 ms and peaked at 1.06 ms. It recorded 401 path requests (13.36/s, at most five/frame), at most six AI commands/frame, and about 5.38 KB managed allocation/frame on average. Audio sources were inactive with `-nographics`. These measurements do not reproduce or explain the earlier graphical 14–24 FPS period; a controlled graphical CPU/GPU capture is still needed before calling the x10 degradation resolved.

An additional x10 batch match initialized Direct3D 12 on the NVIDIA GeForce RTX 3080 Laptop GPU (no `-nographics`). It ended in RedVictory at 853.3 game seconds / 85.3 real seconds, with 7.70 ms mean frame, 29.1 ms worst frame, zero frames above 33 ms, 454 path requests, 357 AI command attempts, and zero runtime errors (`Logs/Phase7E8-GraphicsBatch-Phase7E8Match-x10-20261029-160849.json`). Its 30.0-second marker profile (`Logs/Phase7E8-GraphicsBatch-Profiler.json`) measured 8.12 ms mean / 24.6 ms worst frame, 341 path requests, and at most six AI commands in one frame. The graphics device was initialized, but batch mode has no interactive Game View; this still does not reproduce or explain the earlier Game View drop.

The focused contest fixture exercised actual mine ejection and claiming. Blue contested a Red mine and completed a Blue Castle haul (`Logs/Phase7E8-BlueContest-1.log`). In the reciprocal scenario, a Blue Pawn first claimed the mine, a ready Red military force ejected it, and Red's existing worker policy assigned a Pawn to reclaim it in two consecutive runs (`Logs/Phase7E8-RedRecapture-10.log`, `Logs/Phase7E8-RedRecapture-11.log`). A fixture-driven Red Warrior supplied vision along navigation-valid cells; it did not reveal hidden state directly or issue the miner command. This does not establish autonomous Red tactical approach against live Goblins or an active human Blue army. One teardown-only `MissingReferenceException` exposed a destroyed Unity object in `MilitaryCommanderPolicy.EndRetreatDirectives`; the null guard was corrected and the recapture fixture then completed without that exception.

An additional headless Play Mode assault placed six valid Red Warriors and two valid Blue Warriors around a Red-owned mine. The Blue Warriors received normal combat attack orders; both sides dealt damage, the Red defenders held the mine, and the mine's remaining Gold decreased again after combat (`Logs/Phase7E8-ActiveAssault-Recovery.log`). The same fixture ran the Phase 6C Match Flow validator and destroyed a damaged construction site to verify navigation occupancy release. Smaller diagnostic forces of three or four Red Warriors failed to defend: Red retreated, Blue remained within the mine control radius, and the Red miner switched to another resource. Those runs were below the Red commander's configured offensive population gate and remain useful evidence of insufficient-force behavior, not successful defense. The ready-force test does not represent autonomous Blue strategy.

## Fog fairness and Phase 8 readiness

Static inspection found `DebugObserverView` use in display and human input code; `FactionKnowledge.Refresh` checks `TeamVision.IsVisible` before copying enemy unit, building, resource, Goblin, and mine state. Existing structural validators cover Scout/Fog contracts. This is not yet a full adversarial fog-leak test.

The shared `FactionKnowledge` and `FactionCommandService` interfaces provide a suitable boundary for later strategic RL observations and actions. Automated match startup works in the isolated copy. A deterministic reset contract and repeatable training fixtures still need separate Phase 8 work; the intermittent graphical slowdown should be profiled if it recurs. No PPO or reward function was implemented here.

## Gate verdict

`READY FOR PHASE 8` under the user's narrowed Phase 7E.8 acceptance scope: investigate x10 FPS, test contested-mine cases, and repair headless startup. Gold logistics, reciprocal ownership/worker assignment, active Blue combat with Red mining recovery, headless startup, 14/14 structural checks, and full x1/x2/x5/x10 matches passed. The intermittent Game View slowdown did not recur at the same game time or in repeated x10 runs; its original cause remains unknown. Focused contest fixtures do not replace an autonomous Blue-versus-Red battle. No balance values were changed because these observations do not justify one.

## Outstanding validation

- Compare game-time differences across x1/x2/x5/x10 with multiple controlled seeds. One full match at each slower speed and several x10 matches have passed, but stochastic match length varies.
- Reproduce the earlier x10 FPS degradation under a longer controlled CPU/GPU profile. The 30-second marker capture did not reproduce it or isolate a cause.
- Verify destruction occupancy for a completed building and click the Match End Restart control in an interactive session. The building-site callback and Match Flow mapping validator already passed.
- Run a contested full match against an autonomous Blue commander or active human player. Current focused scenarios exercise both ownership directions and active combat, but Blue's attack orders are fixture-driven.
- The headless fixture startup is operational in the isolated project, but Unity Search still emits Editor-only indexing exceptions. Confirm repeatability in the primary project after its current Editor session is closed.
- `git diff --check` across the entire dirty worktree reports preexisting trailing whitespace in many unrelated files; the Phase 7E.8 source file has no trailing whitespace.
