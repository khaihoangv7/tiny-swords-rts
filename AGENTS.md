# Tiny Kingdom RL - Codex Instructions

## Mandatory reading

Before writing or modifying code, read:

1. `GAME_SPEC.md`
2. `MAP_SPEC.md`
3. `RL_SPEC.md` if the task involves AI or Reinforcement Learning.

These documents define the intended game architecture and gameplay.

Do not invent new game mechanics that conflict with these specifications.

---

## Project

This is a simplified RTS inspired by Age of Empires.

Engine:
Unity

Language:
C#

Art:
Tiny Swords by Pixel Frog.

The project is also intended to support Reinforcement Learning later.

---

## Core rule

The game must work completely without Reinforcement Learning first.

Development order:

1. Map
2. Navigation
3. Unit movement
4. Combat
5. Resources
6. Buildings
7. Production
8. Win/Lose system
9. Rule-based AI
10. RL integration

Do not skip ahead unless explicitly requested.

---

## Architecture rules

Keep systems modular.

Do not create one giant GameManager.

Prefer separate systems such as:

- GameManager
- GridManager
- PathfindingSystem
- SelectionManager
- ResourceManager
- CombatSystem
- BuildingSystem
- ProductionSystem
- FactionManager
- CommanderAI

Use ScriptableObjects for configurable gameplay data.

Do not hard-code unit stats throughout scripts.

---

## Map rules

Tiny Swords terrain uses a 64x64 pixel tile grid.

Visual Tilemaps and logical navigation data must be separate.

Read `MAP_SPEC.md` before modifying map, terrain, tilemaps, navigation or pathfinding.

---

## Gameplay rules

Read `GAME_SPEC.md` before changing:

- units
- resources
- buildings
- combat
- population
- victory conditions
- economy

The specification is the source of truth.

If existing code conflicts with the specification, report the conflict before rewriting large systems.

---

## Reinforcement Learning

Reinforcement Learning must control high-level strategy only.

RL should NOT directly control each unit's individual movement.

Individual units are controlled by normal game logic.

The RL Commander may eventually decide things such as:

- train units
- build structures
- prioritize resources
- attack
- defend
- retreat

Read `RL_SPEC.md` before implementing RL.

---

## Coding workflow

Before making a significant change:

1. Inspect the existing project.
2. Read the relevant specification.
3. Identify affected files.
4. Explain the intended change briefly.
5. Implement the smallest working solution.
6. Compile the project.
7. Fix compiler errors.
8. Avoid unrelated refactors.

Do not rewrite working systems unless necessary.

---

## Scope

This is a student project.

Prefer simple, understandable implementations over overly complex systems.

Do not add unnecessary packages or dependencies.

Do not implement features that were not requested.

When uncertain about a gameplay rule, check the specification files first.
## Asset and Animation Rules

Before implementing or modifying:

- animation
- Animator Controllers
- unit visuals
- resource visuals
- building visuals
- faction visuals

you MUST read `ASSET_MANIFEST.md`.

Never guess which Tiny Swords animation represents a gameplay action.

Before using an animation:

1. Inspect the actual imported asset.
2. Verify the sprite sheet / animation.
3. Match it to a gameplay state.
4. Record the mapping in `ASSET_MANIFEST.md`.
5. Then implement it.

Gameplay state is the source of truth.

Animation must never control gameplay logic.