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

The verified Tiny Swords `SmallBar_Base` three-piece border and
`SmallBar_Fill` red stripe form a small health bar above each combat-capable
unit, including Pawn. It is hidden at full HP and on death, and follows
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
