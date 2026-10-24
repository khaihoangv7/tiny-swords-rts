# Tiny Kingdom RL - Audio Specification

## Authority and scope

Audio is presentation only. Combat, gathering, construction, repair, production,
selection and match flow complete independently of clip playback or clip length.
`GameplayAudioService` is the single runtime audio authority and loads one
serialized `GameplayAudioCatalog` from `Resources`.

## Source pooling

- The service creates 12 reusable one-shot sources initially.
- When all one-shot sources are active, the pool grows by four and reuses those
  sources for later events.
- Legitimate simultaneous instances of the same event or clip are never rejected.
- No per-event concurrency cap, duplicate cooldown or same-clip suppression exists.
- A global 256-source catastrophic safety limit diagnoses accidental event spam; it
  is not expected to affect normal gameplay.
- Fire loops use a separate reusable loop pool and one owner/event handle per
  building. Multiple buildings may use the same fire clip simultaneously.

## World and player-feedback audio

World audio uses the Main Camera AudioListener, full spatial blend, linear rolloff,
and the catalog min/max distance. A world sound only plays while its source is
inside the camera viewport. Enemy or neutral world audio also requires current
Blue visibility through `TeamVisionSystem`. Moving the camera away or losing legal
vision stops that source.

The following are 2D player feedback: building selection, command accepted,
command rejected, successful construction-plan placement, Human command voice,
own unit production completion, victory and defeat.

## Event mappings

| Event | Asset | Trigger | Type |
| --- | --- | --- | --- |
| CastleSelect | `Assets/Audio/SFX/Buildings/Sound Effect/castle_sound.mp3` | Successful Human selection click | 2D one-shot |
| HouseSelect | `Assets/Audio/SFX/Buildings/Sound Effect/house_sound.mp3` | Successful Human selection click | 2D one-shot |
| BarracksSelect | `Assets/Audio/SFX/Buildings/Sound Effect/barracks_sound.mp3` | Successful Human selection click | 2D one-shot |
| ArcherySelect | `Assets/Audio/SFX/Buildings/Sound Effect/archery_sound.mp3` | Successful Human selection click | 2D one-shot |
| MonasterySelect | `Assets/Audio/SFX/Buildings/Sound Effect/monastery_sound.mp3` | Successful Human selection click | 2D one-shot |
| TowerSelect | `Assets/Audio/SFX/Buildings/Sound Effect/tower_sound.mp3` | Successful Human selection click | 2D one-shot |
| BuildingFire | `Assets/Audio/SFX/Buildings/Destruction/fire.mp3` | Observable completed building below 60% HP | World loop |
| BuildingExplosion | `Assets/Audio/SFX/Buildings/Destruction/building_destroy.mp3` | Observable authoritative building death | World one-shot |
| BuildingPlan | `Assets/Audio/SFX/Buildings/Construction/building_plan.mp3` | Successful Human construction placement | 2D one-shot |
| BuildingCompleted | `Assets/Audio/SFX/Buildings/Construction/building_completed.mp3` | First normal construction completion transition | World one-shot |
| ArcherAttack | `Assets/Audio/SFX/Units/Archer/sfx_arrow_sound.mp3` | Actual Archer projectile creation | World one-shot |
| ArcherArrowImpact | `Assets/Audio/SFX/Units/Archer/sfx_arrow_dmg.mp3` | Successful Archer projectile damage to a unit | World one-shot |
| WarriorAttack | `Assets/Audio/SFX/Units/Warrior/sfx_sword_hit_01.mp3` through `_07.mp3` | Successful Warrior damage to a Warrior, Lancer or Archer | World one-shot variation |
| WarriorAttackAlternate | `Assets/Audio/SFX/Units/Warrior/sfx_warrior_attack_01.mp3` | Successful Warrior damage to a Pawn, Monk or building | World one-shot |
| PawnHammer | `Assets/Audio/SFX/Units/Pawn/sfx_pawn_building_01.mp3` and `_02.mp3` | Construction cycle or positive repair impact | World one-shot variation |
| CommandClick | `Assets/Audio/SFX/UI/Notification/click.mp3` | Accepted bottom-panel command click | 2D one-shot |
| CommandRejected | `Assets/Audio/SFX/UI/Notification/cant_click.mp3` | Disabled or rejected bottom-panel command click | 2D one-shot |
| UnitProductionFinished | `Assets/Audio/SFX/UI/Notification/finish_queue.mp3` | Human unit successfully spawns | 2D one-shot |
| GenericCommandVoice | `Assets/Audio/SFX/Commands/units_say_01.mp3` | Accepted non-army Human unit command; 30% / 30s | 2D one-shot |
| ArmyCommandVoice | `Assets/Audio/SFX/Commands/army_say_01.mp3` | Accepted Human command to more than three combat recipients; 40% / 10s | 2D one-shot |
| Victory | `Assets/Audio/SFX/Match/win.mp3` | Authoritative BlueVictory | 2D one-shot |
| Defeat | `Assets/Audio/SFX/Match/defeat.mp3` | Authoritative RedVictory | 2D one-shot |

Gold Mine has no corresponding selection clip in the supplied directory, so it
does not fabricate or reuse another building's acknowledgement.

## Building fire and destruction

`BuildingDamagePresentation` owns the shared VFX/audio damaged state. Completed,
alive buildings start burning below 0.60 HP ratio and stop at or above 0.60. Each
audible building starts one loop and does not restart it on later damage events.
Repairing across the threshold stops and releases the loop. Destruction stops the
loop before requesting one explosion sound.

Friendly building audio follows the existing own-faction presentation rule. Hidden
enemy fire and explosions are silent. If a burning enemy leaves current Blue
vision, its loop stops. It may start again only after the building is currently
visible and its current HP can legally be observed. Match End stops all fire loops.

## Command and production notifications

Command buttons reuse their authoritative availability/action result. Accepted
commands request `click.mp3`; disabled or failed commands request `cant_click.mp3`.
The two cues are mutually exclusive for one click. A build-button click is separate
from a later successful placement, which requests `building_plan.mp3`.

`finish_queue.mp3` occurs only after successful spawn from a production queue owned
by the current Human view faction. Enqueue does not trigger it and hidden Red
production cannot notify Blue.

Human unit command voice is evaluated only in the same input call as a direct
world-command mouse click, and only after at least one selected unit accepts the
command. This covers Move, Gather, Attack, Attack Move, Heal, Garrison and
construction/repair assignments. Formation selection from the command panel,
programmatic command calls and AI decisions stay silent. Army count includes
accepted living Warrior, Lancer and Archer units;
Pawn, Pawn Scout and Monk are excluded from that count. The army and generic groups
are mutually exclusive and keep independent cooldowns measured with scaled game
time. A failed probability roll, missing clip, unavailable source or rejected
command does not begin a cooldown. AI command paths do not invoke this policy.

## Match lifecycle

`MatchManager.MatchEnded` is authoritative. One match result requests exactly one
Victory or Defeat cue, independent of Fog and camera position. Scene load/restart
clears loop ownership, active sources and the once-per-match result guard.

## Import and tuning

Short SFX use Decompress On Load and preload. The 36.52-second fire loop uses
Streaming, background loading and no preload. The 8.10-second win and 11.76-second
defeat themes use Compressed In Memory with preload. Volumes remain configurable per
catalog event. Current values are provisional presentation tuning.

## Known unavailable and deferred audio

- No Gold Mine selection clip exists in the supplied building-selection directory.
- Sheep Hurt is intentionally not requested by the current game and its former clip
  is absent from the working asset set, matching the earlier removal request.
- The former `attack_theme.mp3` was removed; no background music is assigned.
- Food harvesting, generic hit/death, ambient music and an options/mixer UI remain
  future audio work.
