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

## Floating damage

After actual HP loss, show that amount above the target. Move it upward, fade
it out, and remove it. Distinguish a hit reduced by Guard with a shield/color
cue. Text has no collision and never determines damage, death, or timing.
