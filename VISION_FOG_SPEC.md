# Tiny Kingdom RL — Vision and Fog of War

## Phase 6A rules

`TeamVisionSystem` is the authoritative gameplay source for visibility. The fog
texture is presentation only and must never be sampled by gameplay code.

Each faction owns independent state for every cell of the existing 40x28 logical
grid:

- `Visible`: inside the circular radius of one or more active friendly sources.
- `Explored`: seen earlier in the match, but not currently visible.
- `Unexplored`: never seen by that faction.

Blue and Red current visibility and exploration history are stored separately.
Exploration memory lasts until the current match ends.

## Vision sources

Current configurable MVP radii:

| Source | VisionRadius |
|---|---:|
| Pawn | 3.0 |
| Warrior | 3.5 |
| Archer | 4.0 |
| Lancer | 3.5 |
| House | 5 |
| Barracks | 5 |
| Archery | 5 |
| Monastery | 5 |
| Tower | 7 |
| Castle | 9 |

Phase 6A balance gives Archer slightly more scouting vision than the melee
units, while buildings retain stronger strategic vision. The nondefensive
building value 5 remains provisional. Tower remains 7; Phase 6B.1 increases
the main Castle from 8 to 9. VisionRadius is independent from attack range and
aggro radius.

Rally Point placement uses the same faction exploration map. Friendly
Rally-enabled buildings may accept valid ground in `Visible` or `Explored`
cells, but reject `Unexplored` cells. The selection-only Rally flag is
presentation and does not reveal terrain or contribute vision.

Living world units and completed living buildings provide vision. Dead units,
destroyed buildings and buildings under construction do not. Internal garrison
occupants and mounted Archers do not add independent vision; the host building
radius is unchanged by occupants.

Vision uses distance from each logical cell center to the source world position.
Cells whose centers are within the radius are visible. The union of all active
same-faction sources is recomputed every 0.10 seconds.

## Presentation

The current human view faction is Blue. `FogRenderer` can display either faction
for Editor debugging without changing authoritative data.

The world-space fog is one point-filtered 40x28 texture aligned with the logical
grid:

- Visible: transparent.
- Explored: black with alpha 135/255.
- Unexplored: black with alpha 235/255.

It renders at sorting order 8, above terrain and below gameplay sprites. Friendly
objects remain clear. Enemy units and buildings are presentation-gated and appear
only while currently Visible. Their world-space health, construction, selection
and garrison renderers are gated with the same object. Hidden enemies cannot be
hovered or targeted by human input.

Resources are hidden while Unexplored. Phase 6C.1 replaces the earlier live
Explored presentation with static, faction-specific last-seen snapshots.
Ambient Gold Highlight and hidden Sheep movement never update a remembered
resource presentation.

Fog renderers have no collider, raycast target or navigation role.

## Phase 6B combat awareness

Normal unit and building combat queries the authoritative faction state through
`CombatVisibility`. Passive aggro, AttackMove, explicit Attack continuation,
mounted Archer targeting and Tower/Castle defensive arrows require a target to
be currently Visible to the observer faction. Allied vision is shared, so a
Tower or another unit may provide the visibility needed by an attacker. Combat
does not calculate a second visibility radius and never reads renderers or fog
texture opacity.

A normal target that becomes hidden is dropped. AttackMove resumes its saved
destination; an unreleased ranged windup is cancelled; melee reservations are
released. There is no last-known-position pursuit in this phase. A projectile
already released while authorized remains valid and may resolve after vision
loss. Each new shot, windup or defensive volley requires current visibility.

Warrior, Archer and Lancer retain a local direct-damage retaliation exception.
The damaged unit alone may track its attacker outside faction vision while the
existing target rules and RetaliationLeash 10 remain valid. This exception never
marks cells Visible, changes explored memory, reveals presentation, or shares
the attacker with allies. Pawn and Monk remain excluded from auto retaliation.

## Phase 6C.1 faction memory and player knowledge

The three visibility states have the following information contract:

- `Visible`: show and query current live world truth.
- `Explored`: show only the last-seen static snapshot for enemy buildings and
  resources.
- `Unexplored`: provide no object knowledge or object presentation.

`FogMemorySystem` owns separate Blue and Red dictionaries for building and
resource snapshots. A snapshot is created or replaced only while its object is
currently `Visible` to that faction. Hidden repairs, damage, destruction,
construction, production, garrison state, Fire, Rally flags, Gold Highlight,
resource depletion and Sheep movement cannot update it. When the remembered
cell becomes Visible again, the snapshot updates from the observed object or is
removed if that object is no longer present. Building ghosts contain only the
last-seen building type, faction, logical position/footprint and frozen sprite
transform/color plus recorded construction and health ratios. Resource ghosts
contain only type, logical position/cell, frozen sprite transform/color and
recorded remaining ratio.

Ghosts are static presentation objects without colliders, selection, combat,
health bars, garrison UI, navigation occupancy or vision. Enemy units never
receive persistent ghosts: they disappear completely after leaving current
vision. The query API exposes current visibility, exploration history and
snapshot values rather than hidden live component references.

Player hover, cursor, selection and attack checks require current observability.
Last-seen ghosts cannot be targeted. Player-facing Move and Rally validation use
faction knowledge, so an unobserved physical building cannot cause a Forbidden
cursor or rejected command. Authoritative A* still uses real world occupancy and
routes around the blocker. Rally remains allowed on `Visible` and `Explored`
known-valid ground and rejected on `Unexplored` ground.

## Deferred to a later phase

- Terrain, cliff, tree, unit and building line-of-sight occlusion.
- Stealth, camouflage, detection and minimap.
- AI and Reinforcement Learning observations, actions or rewards.
