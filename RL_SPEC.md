# Tiny Kingdom RL - Reinforcement Learning Specification

## Purpose

The RL agent acts as a strategic Commander.

It does NOT directly control individual units.

---

## Low-level control

Handled by normal game systems:

- movement
- A* pathfinding
- target selection
- attacking
- gathering
- returning resources
- building

---

## RL Observation

The future RL agent may observe:

- Food
- Wood
- Gold

- Population
- Population Cap

- Pawn count
- Warrior count
- Archer count
- Lancer count

- Enemy Warrior count
- Enemy Archer count
- Enemy Lancer count

- Own Castle HP
- Enemy Castle HP

- Own army strength
- Enemy army strength

- game time

---

## RL Actions

Possible strategic actions:

- TrainPawn
- TrainWarrior
- TrainArcher
- TrainLancer

- BuildHouse
- BuildBarracks
- BuildTower

- PrioritizeFood
- PrioritizeWood
- PrioritizeGold

- Attack
- Defend
- Retreat

- Wait

---

## Reward

Initial reward concept:

Win:
+100

Lose:
-100

Destroy enemy unit:
+5

Lose unit:
-3

Destroy enemy building:
+15

Lose building:
-15

Damage enemy Castle:
positive proportional reward

Own Castle damage:
negative proportional reward

Small time penalty:
discourage endless games.

These values are experimental and may be tuned later.