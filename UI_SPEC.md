# Tiny Kingdom RL — Unit Presentation UI

This specification covers the current combat test scene only. Gameplay remains
authoritative; UI and animation only observe gameplay.

## Selection summary

Show one row per selected UnitType, with verified icon or an Idle-sprite portrait,
type name, and count. Refresh immediately when selection changes or a selected
unit dies. For exactly one selected unit also display CurrentHP / MaxHP and
refresh on HP changes.

## Guard command

Show the Guard button when at least one Warrior is selected. Use the Tiny Swords
shield icon `Assets/UI Elements/UI Elements/Icons/Icon_06.png` and label it
`Guard (G)`. Pressing and holding the button or G guards all eligible selected
Warriors. Releasing either input releases Warriors guarded by that input, even
when the pointer is outside the button. Other selected unit types are ignored.
Show an active appearance while at least one selected Warrior is Guarding.
A new Move or Attack order cancels Guard and does not reactivate it until a new
Guard press. The button does not issue a world selection or movement order.

## Phase 5A combat feedback

Normal combat damage numbers are disabled. A unit with actual HP loss briefly
flashes red, then white, then returns to its normal sprite. The flash overlays
the current animation frame and never affects gameplay damage or timing.

The verified Tiny Swords `SmallBar_Base` three-piece border and the runtime-tinted
`SmallBar_Fill` stripe form a small health bar above each combat-capable unit,
including Pawn. It is hidden at full HP and on death, and follows
`HealthComponent.CurrentHP / MaxHP` immediately after HP changes. It has no
collider or UI raycast target.

## Phase 5A resource feedback

After a successful gather impact, a verified Food, Wood, or Gold icon and the
actual `+amount` appear above the Pawn, rise slightly, fade, and self-remove.
After stockpile deposit, the corresponding icon and actual amount appear above
the Castle. These popups observe resource events and never change cargo or
stockpile values.

## Phase 5C1 construction and production commands

Selecting a Pawn shows House A/B/C, Barracks, Tower, Archery and Monastery
build choices. House variants share one gameplay cost and population effect.
Buttons show the building image and cost and are disabled when resources are
insufficient. Placement preview and footprint border show valid/invalid color.

Selecting the Castle shows Train Pawn. Completed Barracks shows Warrior and
Lancer; completed Archery shows Archer. The producer panel shows costs,
availability, FIFO queue and current progress. Monastery shows that Monk is
unavailable until gameplay data exists. House and Tower have no train buttons.
The UI only calls gameplay production APIs; timers, payment, population and
spawn positions are controlled by the production queue.

Phase 6C.2 enables Monk on the Monastery production panel with 60 Food +
60 Gold cost. Selecting an owned Gold Mine shows state, HP, internal Pawn count
out of 3, remaining Gold and Eject All. Right-clicking a damaged friendly unit
with Monk selected issues manual Heal. Right-clicking a neutral or owned
same-faction Gold Mine with Pawn selected issues the mine-entry command.

## Phase 5C2 garrison commands

Right-clicking a friendly completed Tower or Castle with selected units issues
the shared garrison gameplay command. Archer reserves an available rooftop slot
first, then falls back to an internal slot. Pawn, Warrior and Lancer use internal
slots. Units physically approach a reserved interaction position before entry.

Selecting Tower shows `Internal Garrison: current / 2` and
`Mounted Archers: current / 1`. Selecting Castle shows the corresponding 5 and
3 capacities. The compact panel includes `Eject All`. Internal and mounted units
are represented through the host panel and are not world selectable while
protected. Production UI remains available when Castle is selected.

Selecting a currently observable Tree, Gold Stone, Sheep or Gold Mine displays
the same green elliptical ground-selection language used for units. The ring is
presentation-only, follows Fog presentation, hides when selection is cleared or
invalidated, and has no collider, raycast, occupancy or gameplay authority.

Unit selection ellipses scale their width, height and authored ground offset by
the 1.3 unit presentation factor. The ellipse remains a sibling of the animated
visual, so animation and damage deformation cannot pull it away from the feet.

Selecting a friendly House, Barracks, Archery, Monastery, Tower or Castle plays
a brief vertical squash/stretch on building artwork. It has no command,
selection, health or construction authority and preserves the artwork's normal
ground anchor and configured 1.7 scale after recovery.

A successful Pawn repair impact reuses this mild vertical pulse. The pulse is
triggered by the authoritative repair timer at the Hammer impact, not by an
animation frame, and does not alter repair amount or building gameplay state.

When a Pawn carrying resources physically completes internal entry into the
friendly main Castle, the existing Castle drop-off transaction runs before the
Pawn is hidden, including normal resource `+amount` feedback. A cancelled
approach shows no deposit. Tower garrison entry preserves cargo.

## Phase 5C2.1 world feedback

A completed Tower or Castle with at least one internal occupant shows a small
world-space `current/capacity` label above the host. The label reads directly
from `BuildingGarrison`, excludes mounted Archers, follows the building, does
not block raycasts and hides at zero occupants or on destruction.

Every `BuildingSite` under construction shows a fixed dark frame with one
continuous left-anchored construction fill sourced from authoritative
`ConstructionProgress`. It hides at completion. Construction progress, internal
garrison count and damaged-building health use separate presentation objects
and vertically separated positions.

## Phase 6B.1 RTS controls and Rally Points

`A` arms AttackMove for selected Warrior, Archer and Lancer units. Left-clicking
valid ground issues the command; an invalid destination keeps targeting armed,
and Escape cancels it. Right Click while armed cancels AttackMove targeting and
then performs the normal contextual Right Click command.
Camera movement uses Arrow Keys and configurable eight-direction mouse-edge
scrolling. WASD does not move the camera.

Selecting a completed friendly Castle/Home, Barracks, Archery, Monastery or
Tower and right-clicking valid discovered ground assigns that building's Rally
Point. House is excluded. A small world flag is visible only
while its owner building is selected. Invalid, blocked, outside-map and
Unexplored destinations use Forbidden feedback and do not replace the stored
point. Produced units and manually ejected occupants spawn/eject normally,
then receive distributed normal Move destinations around the Rally Point.
Emergency destruction ejection ignores Rally Points.

An accepted explicit player Attack command against an enemy unit creates two
red flattened world-space ellipses under that target's feet. They follow the
target, shrink and fade over 0.42 seconds, render above terrain and below the
unit, and have no collider or gameplay effect. Automatic acquisition and
AttackMove encounters do not create this feedback.

## Phase 6B.2 command and building-damage feedback

Final command controls for this milestone are: Arrow Keys or the mouse screen
edge pan the camera; `A` arms AttackMove; `A` + Left Click valid ground issues
AttackMove; Right Click ground normally issues Move; Right Click enemy normally
issues explicit Attack; and `G` controls Guard for eligible Warriors.

The Default, Hand and Forbidden gameplay cursors use a 1.5 presentation scale.
They are enlarged with nearest-neighbor sampling at runtime, and each verified
hotspot is scaled with its texture so pointing and command coordinates remain
unchanged. A successful normal Move command retains its 0.45-second two-ring
shrink/fade timing while the complete marker and line width render at 1.35 scale.

A successful `A` + Left Click ground AttackMove command creates its own two
red flattened world-space ellipses at the destination. They remain fixed at the
clicked ground point, shrink and fade over about 0.45 seconds, render above
terrain and below units, and never follow a later acquired enemy. Invalid
destinations, passive acquisition and retaliation do not create the marker.

Completed buildings below 70 percent HP show all three verified looping Tiny
Swords Fire effects at separate local anchors. Repair to 70 percent or more
hides Fire immediately. Building destruction stops Fire and plays both verified
Explosion effects once without delaying gameplay destruction. Fog suppresses
enemy damage VFX that the player cannot currently observe.

Damaged buildings use `BigBar_Base` as one static assembled frame and one
left-anchored continuous `BigBar_Fill`. Only fill width changes. Full-HP and
destroyed bars are hidden, while construction progress remains a separate bar.
Unit health bars continue using `SmallBar`. The world garrison count is placed
above the building Life Bar to avoid overlap.

## Phase 6C Victory and Defeat result UI

The screen-space `MatchResultOverlay` appears 0.85 seconds after the
authoritative Main Castle result. A black overlay at 0.55 alpha keeps the
battlefield recognizable while blocking world pointer input. The centered
result panel uses the nine-piece Tiny Swords `SpecialPaper` frame.

Blue human victory uses the Blue `BigRibbons` three-piece ribbon with VICTORY;
defeat uses the matching Red row with DEFEAT. Each result uses the corresponding
Tiny Swords Small Square button Regular and Pressed sprites for Restart, with a
subtle hover tint. The UI reuses the project's existing `LegacyRuntime.ttf`
text style because no separate font asset is imported. Camera controls remain
active, while selection and gameplay command panels are disabled when the
result overlay becomes visible.

Restart reloads the current scene through `SceneManager`, providing a clean
reset of all scene-owned gameplay and visibility state. Result UI strings are
presentation only and are never a gameplay or RL authority.

## Phase 6C.1 Fog memory and Rally presentation

The Rally marker uses the inspected Tiny Swords `Icon_09_0` crossed-weapons
icon as a world-space sprite, following the targeted presentation correction.
One marker is owned by each Rally-enabled building. It
is shown only when that friendly owner is selected, hides on deselection,
moves instead of duplicating when the point changes, and is destroyed with the
owner. It has no collider, raycast, navigation or vision role.

Enemy buildings and resources in current vision use their live presentation.
In Explored Fog, a frozen last-seen sprite may be shown instead. This ghost has
no health bar, construction bar, Fire, garrison count, Rally marker, tooltip,
selection or combat interaction. Enemy units have no remembered presentation.
Hover/cursor logic ignores hidden live objects and never treats a memory ghost
as a live target.

Friendly health fill color is resolved from `ownerFaction == PlayerViewFaction`,
not from a fixed Blue mapping. Friendly SmallBar and Big/Large Life Bar fills use
`Color32(74, 201, 86, 255)` green. Enemy fills use
`Color32(255, 62, 62, 255)` red. Only fill pixels are recolored; frame,
background, left anchoring, width calculation and visibility behavior remain
unchanged. Fog hides the complete live enemy bar, and memory ghosts have no bar.

Positive unit damage and successful Sheep Food harvest impacts use a shared
presentation-only `Squash -> Stretch -> Normal` reaction. The animated body is
wrapped by a dedicated ground-anchored presentation transform; gameplay root,
Shadow, SelectionRing, health bar and labels are not scaled or moved. Hit flash
and the currently playing Idle, Run, Attack, Guard, work or Sheep animation keep
running. Rapid hits restart one reaction from the exact stored baseline.

## Phase UI/UX RTS HUD

The gameplay HUD uses a 1920x1080 reference canvas, Scale With Screen Size,
width/height match 0.5. A horizontally stretched `RtsBottomHud` root occupies
the canvas between 12-unit left/right safe margins and is 188 units high. At the
reference aspect this is 1896x188. The root itself has no Graphic or raycast
target; only each visible panel blocks world input. All three sections share the
same baseline; hiding a contextual panel never moves the resource panel.
Arbitrary Unity Game View scaling is not an image-quality reference; visual
checks use Game View Scale 100 percent.

The WoodTable resource panel is always visible at the lower left. It reads the
authoritative `FactionPopulation` and `ResourceStockpile` values and displays
Population as Used/Cap followed by Wood, Gold and Food. The icons are
`Icon_01_0` through `Icon_04_0` in that order. The panel is 220x188, resource
icons are 34x34, rows are 36 pixels high and bold values use font size 22.

The SpecialPaper selection panel appears only while a selectable object is
selected. A single unit shows its verified avatar, type, faction, HP and current
state; Pawn cargo is supplemental. Multiple units show counts by explicit unit
type. Buildings show type, HP, construction state/progress, garrison counts and
production queue/progress where available. Tree, Sheep, Gold Stone and the
strategic Gold Mine show authoritative remaining/capacity and health data that
the player is allowed to observe under Fog rules. This panel remains 380x188;
single portraits are 96x96 and primary titles use font size 24. Multi-selection
rows adapt inside a fixed 168-unit content region: two rows use 52x52 icons and
font size 21, four rows use 40x40 icons and font size 19, and five rows retain
34x34 icons with font size 18. Detail text remains 16 and queue icons remain
34x34.

The SpecialPaper command panel appears only when the current selection has
valid commands. It invokes existing gameplay APIs for AttackMove, hold-to-Guard,
Pawn building placement, production and Eject All. Costs and unavailable states
come from the existing data and authority checks. The UI never duplicates
payment, population, construction, training, garrison or command authority.
The panel is a single-row adaptive bar with an explicit fixed HUD zone.
Resources remains at x=0 with size 220x188; Selection remains at x=224 with
size 380x188; Command begins at x=608, preserving the original four-unit gap
after Selection. All three use lower-left anchors and pivots with explicit
sizes. No LayoutGroup, LayoutElement or ContentSizeFitter redistributes sibling
width. Command count therefore cannot resize or cover Resources/Selection.
Command width grows only to the right and is clamped between 240 and the safe
right-side region, 1288 units at the 1920x1080 reference geometry.

Cards use a shared range from 104x112 down to 76x82. The panel first grows while
cards stay at 104x112; if the safe maximum is reached, every active card shrinks
to the same calculated width and proportional height. Icons scale from 68 to 48,
command labels from font 15 to 12, secondary cost text from 13 to 10, and
hotkeys from 13 to 10. Horizontal padding is 12 units per side and the gap is 6.
At reference geometry, two commands use a 240-unit panel, six use 678, seven use
788, and 13/14 use the 1288-unit maximum with approximately 91.69x98.81 and
84.71x91.34 cards respectively. The panel height always remains 188 and all
active commands remain in exactly one left-to-right row.
AttackMove uses verified `Icon_05_0`. Eject All uses verified `Icon_09_0` for
Castle, Tower and owned Gold Mine command entries; the legacy compact garrison
button shows the same icon without changing its command behavior.

WoodTable and SpecialPaper are each drawn as one continuous center surface with
eight frame pieces over it. The center and edges use Simple stretching rather
than Tiled repetition. This keeps the panel visually continuous, prevents
transparent sprite trim from showing the battlefield between pieces and keeps
the complete visible panel as one UI raycast-blocking region.

During this UI/UX milestone Goblin Torch/TNT are removed from the active Map
setup and validator requirements. Their imported assets and dormant gameplay
code remain available for later work.

## Phase 6D.3 Command Tooltips and Cursor Interaction Polish

The bottom command grid owns fourteen persistent button objects. Each button has
an EventSystem pointer trigger, while one shared `CommandTooltipPresenter`
creates and reuses a single `CommandTooltip` panel beside the hovered command.
Tooltip graphics do not receive raycasts, so the panel cannot interrupt the
button's pointer enter/exit lifecycle. Selection changes, hidden/reused buttons,
match end and HUD teardown clear the current tooltip.

The tooltip has a 240 UI unit minimum width, 286 preferred width and 320 maximum
width. Train and Build commands normally use the preferred width; simple command
help may use the minimum, while a wider cost row can grow only up to the maximum.
Height is recalculated from the visible Title, Cost, Stats and Details preferred
heights with 12 units horizontal padding, 10 vertical padding and 5 between
sections. Empty sections are inactive and consume no height. The SpecialPaper
surface and eight frame pieces stretch with the resulting root rectangle.

While visible, the presenter converts the Input System pointer position into the
bottom HUD root's local coordinates with `RectTransformUtility`. A 30 UI unit
offset places the tooltip above-right by default. It flips left or below near the
corresponding screen edge, then clamps the complete rectangle inside an 8-unit
safe margin. The fixed bottom-left pivot avoids content-dependent pivot jumps.
Only position updates each visible frame; content and preferred sizes update on
pointer entry or command source change. A non-interactable, non-blocking
`CanvasGroup` and non-raycast graphics prevent hover flicker and blocked clicks.

Train tooltips read cost, population, HP, damage, range, speed, attack interval
and training duration from the same `UnitCombatData` used by production and
combat. Warrior, Archer and Lancer strengths and weaknesses are derived from
`CombatDamage.Multiplier`; they are not a second UI matchup table. Pawn is
described as a worker. Monk displays the authoritative healing amount, range
and interval from `MonkHealer` and does not claim a fabricated combat counter.

Build tooltips read cost, HP, logical footprint, construction duration and
population bonus or defense capacity from `BuildingData`. Production building
rosters are derived through `UnitProductionQueue.CanProduce`. Cost rows reuse
the Food, Wood, Gold and Population sprites in `RtsHudAssetCatalog`. A missing
resource or population capacity colors only the affected cost value; tooltip
code never pays resources, reserves population or changes command authority.
Disabled Train and Build buttons remain raycastable by their tooltip trigger.

World cursor resolution gives explicit placement and AttackMove targeting first
priority. Outside an explicit target mode, a currently observable selectable
friendly unit/building uses the existing Hand cursor and ordinary terrain uses
the Default cursor. Merely selecting a production building does not create a
rally targeting mode; contextual right click still sets a valid rally point.
UI raycast blocking and existing Fog visibility rules run before world hover,
so hidden enemy objects cannot be revealed by cursor state.

## Phase 6D.4 Interaction Cursor and Construction Animation Polish

Outside an explicit Placement or AttackMove mode, normal world hover resolves a
legally observable selectable entity before testing whether the raw mouse cell is
a valid movement destination. Sheep, Tree, Gold resources and friendly buildings
use a query-only `SelectionHitArea` trigger fitted to the current sprite bounds.
Animated resource sprites resynchronize this collider after a frame/pivot change.
World SpriteRenderer bounds are converted into the hit-area transform's local
collider space, including presentation scale and child offset. Sheep applies a
symmetrical 0.08-world-unit selection-only padding so its exact bounds edge does not
fall through to terrain when the cursor hotspot sits on visible wool pixels. This allows the Hand
cursor and normal click resolution across Sheep wool, Tree canopy/trunk, Gold art
and Castle side edges. The trigger does not replace the root gameplay collider and
has no navigation, occupancy, combat-range, resource-slot or building-placement
authority.

UI blocking remains first, while explicit target modes continue to override normal
entity hover. Resource visibility is checked through the current Fog presentation
rule, and friendly building hover still checks faction/availability, so hidden live
objects cannot disclose themselves through the cursor.

Pawn construction presentation observes `PawnBuilder.IsWorking`. A Pawn walking to
its reserved interaction slot uses RunHammer; an assigned Pawn waiting at the site
uses IdleHammer; and every Pawn that has arrived and is contributing loops the
verified Hammer animation. Builder arrival accepts a 0.2-world-unit terminal
tolerance so a small final UnitSeparation displacement cannot leave secondary
Pawns permanently holding IdleHammer beside their reserved slots. Movement must
still be complete and each Pawn must own a valid slot. Leaving, death, reassignment,
completion or loss of a valid work target clears the working state through the
existing builder lifecycle. Animation never advances construction and does not
trigger hammer audio. The existing per-Pawn authoritative progress update, build
duration, interaction slots and work-cycle SFX remain unchanged.

## Phase 6D.5 HUD Scale and Adaptive Command Panel Polish

All fourteen command action objects remain pooled and reusable. Only active
actions participate in presentation. For `N` active actions and safe width `S`,
card width is `clamp((S - 2*12 - (N-1)*6) / N, 76, 104)`. Card height and all
internal metrics interpolate across their documented minimum/maximum range.
Panel width is `clamp(2*12 + N*cardWidth + (N-1)*6, 240, S)`. A dedicated
`CommandContent` RectTransform stretches inside the frame with 12-unit side
insets. All action objects are children of that content rect and use the same
final visible count, card width and row origin, so their complete bounds remain
inside both content and panel. The current frame, continuous background and
raycast blocker resize together. The title remains stable at font 18 and
centered on the current frame.

The presenter recalculates geometry only when active count or parent canvas
width changes. Selection refreshes that only change affordability or formation
tint reuse existing RectTransforms without layout work. Screen-width changes,
domain reload and a different visible count restore the same one-row result.
No GridLayoutGroup, HorizontalLayoutGroup, ScrollRect, paging control or action
reordering is used.

The shared command geometry applies equally to Build, Train, AttackMove, Guard,
Eject All and Compact/Wedge/Loose/Line actions. The procedural formation dots
use the same enlarged command interior. Tooltip triggers, click/reject audio,
disabled-button hover, UI raycast blocking and domain-reload rebinding remain on
the same pooled objects.

Resources, Selection and Command panels retain their verified WoodTable and
SpecialPaper surfaces plus eight independent frame pieces. Resource/Selection
keep their bottom-left anchors and Selection keeps its 380x188 outer frame.
Command uses a stable lower-left anchor/pivot at HUD x=608 and resizes its real
surface/raycast rectangle only toward the right. The global
1920x1080 CanvasScaler is unchanged; result UI,
tooltips, cursors and other canvases therefore receive no indirect scale change.
No gameplay value, command eligibility or formation rule is affected.

## Phase 6G.2 Formation Commands

When at least two living, friendly, controllable military units are selected, the
existing bottom Command Panel shows Compact, Wedge, Loose and Line formation
commands. Military formation eligibility is explicitly limited to Warrior, Lancer,
Archer and Monk. Pawn does not count toward the two-unit minimum, so Pawn-only and
one-military-plus-Pawn selections show no formation buttons or formation tooltip
targets. Mixed selections with at least two military units show the controls, but
the resulting reform affects only those military units. The selected formation is
shown with a distinct pale-green command-slot tint, and the preference remains
active when the player changes selections.

Inspection of the imported Tiny Swords `Icon_01` through `Icon_12` set found no
formation-specific art. Each command therefore draws a lightweight six-dot UI
glyph: a block for Compact, widening rows for Wedge, spaced dots for Loose and one
lateral row for Line. These glyphs are presentation only and introduce no external
art. Each button reuses the Phase 6D.3 tooltip presenter and concise shape help.
Clicks use the existing Phase 6E `CommandClick`/`CommandRejected` request path.
No formation-specific sound is added.
